import io
import os
import sys
import json
import logging
import traceback
from datetime import datetime
from pathlib import Path
from typing import Dict, List, Optional, Any

from flask import Flask, request, jsonify, send_file
from flask_cors import CORS

import google.generativeai as genai
from langchain.docstore.document import Document
from langchain.text_splitter import RecursiveCharacterTextSplitter
from langchain_community.vectorstores import FAISS
from langchain_community.embeddings import HuggingFaceEmbeddings
import pytesseract
import fitz  # PyMuPDF
from pdf2image import convert_from_path
from PIL import Image
import numpy as np
import cv2
import tempfile
import shutil
from fpdf import FPDF

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler('pdf_qa_api.log'),
        logging.StreamHandler(sys.stdout)
    ]
)
logger = logging.getLogger(__name__)

# Initialize Flask app
app = Flask(__name__)
CORS(app)


class PdfQAProcessor:
    def __init__(self, config_path: str = "config.json"):
        self.config = self._load_config(config_path)
        self.setup_gemini()
        self.setup_embeddings()
        self.documents = {}  # Store langchain documents
        self.vectorstores = {}  # Store FAISS vectorstores
        self.qa_log = []  # Q&A history log
        self.document_metadata = {}  # Store document metadata for better tracking

    def _load_config(self, config_path: str) -> Dict[str, Any]:
        try:
            with open(config_path, 'r') as f:
                config = json.load(f)
            logger.info(f"Configuration loaded from {config_path}")
            return config
        except FileNotFoundError:
            logger.error(f"Configuration file {config_path} not found")
            return {
                "gemini_api_key": "",
                "model_name": "models/gemini-1.5-flash",
                "embedding_model": "all-MiniLM-L6-v2",
                "max_file_size_mb": 50,
                "supported_formats": [".pdf"],
                "upload_folder": "./uploads",
                "temp_folder": "./temp"
            }

    def setup_gemini(self):
        try:
            api_key = self.config.get("gemini_api_key", "")
            if not api_key:
                raise ValueError("Gemini API key not found in configuration")
            genai.configure(api_key=api_key)
            self.model = genai.GenerativeModel(self.config.get("model_name", "models/gemini-1.5-flash"))
            logger.info("Gemini API initialized successfully")
        except Exception as e:
            logger.error(f"Failed to setup Gemini API: {str(e)}")
            raise

    def setup_embeddings(self):
        try:
            model_name = self.config.get("embedding_model", "all-MiniLM-L6-v2")
            self.embedding_model = HuggingFaceEmbeddings(model_name=model_name)
            logger.info(f"Embedding model '{model_name}' loaded successfully")
        except Exception as e:
            logger.error(f"Failed to setup embeddings: {str(e)}")
            raise

    def preprocess_image_for_ocr(self, pil_image):
        """Image preprocessing for OCR"""
        img = np.array(pil_image.convert('L'))  # Grayscale
        img = cv2.adaptiveThreshold(img, 255, cv2.ADAPTIVE_THRESH_MEAN_C,
                                    cv2.THRESH_BINARY, 11, 12)  # Binarize
        img = cv2.medianBlur(img, 3)  # Denoise
        return Image.fromarray(img)

    def extract_text_from_pdf(self, pdf_path: str, document_name: str) -> List[Document]:
        """Extract text from PDF with enhanced metadata tracking"""
        try:
            logger.info(f"Starting text extraction from {pdf_path}")
            all_docs = []
            
            # Store document metadata
            pdf_info = {
                "filename": document_name,
                "full_path": pdf_path,
                "processed_at": datetime.now().isoformat()
            }
            
            doc = fitz.open(pdf_path)
            total_pages = len(doc)
            pdf_info["total_pages"] = total_pages
            
            for page_num, page in enumerate(doc, start=1):
                text = page.get_text()
                extraction_method = "direct"

                # Use OCR if no text found
                if not text.strip():
                    logger.info(f"Using OCR for Page {page_num} in {document_name}")
                    extraction_method = "ocr"
                    images = convert_from_path(pdf_path, first_page=page_num, last_page=page_num)
                    processed_image = self.preprocess_image_for_ocr(images[0])
                    text = pytesseract.image_to_string(processed_image, config='--psm 6')

                    # Check OCR confidence
                    ocr_data = pytesseract.image_to_data(processed_image, output_type=pytesseract.Output.DICT)
                    confidences = []
                    for conf in ocr_data["conf"]:
                        try:
                            confidences.append(int(conf))
                        except (ValueError, TypeError):
                            continue
                    avg_conf = sum(confidences) / len(confidences) if confidences else 0
                    if avg_conf < 50:
                        logger.warning(f"OCR confidence low ({avg_conf:.1f}) on Page {page_num} of {document_name}")

                # Extract section title - improved detection
                section_title = "Unknown"
                lines = text.split("\n")
                for line in lines[:15]:  # Check first 15 lines
                    line = line.strip()
                    if line and len(line) > 5 and len(line) < 80:
                        # Check if it's a title (all caps, title case, or has specific markers)
                        if (line.isupper() or 
                            line.istitle() or 
                            any(marker in line.lower() for marker in ['chapter', 'section', 'part', 'introduction', 'conclusion'])):
                            section_title = line
                            break

                # Skip if no meaningful text
                if not text.strip() or len(text.strip()) < 20:
                    continue

                # Split text into chunks
                splitter = RecursiveCharacterTextSplitter(
                    chunk_size=800, 
                    chunk_overlap=200,
                    separators=["\n\n", "\n", ". ", " ", ""]
                )
                chunks = splitter.split_text(text)

                for chunk_idx, chunk in enumerate(chunks):
                    if chunk.strip() and len(chunk.strip()) > 10:  # Only add meaningful chunks
                        metadata = {
                            "source": document_name,
                            "source_path": pdf_path,
                            "page_number": page_num,
                            "total_pages": total_pages,
                            "section": section_title,
                            "chunk_index": chunk_idx,
                            "extraction_method": extraction_method,
                            "processed_at": datetime.now().isoformat()
                        }
                        all_docs.append(Document(page_content=chunk, metadata=metadata))

            doc.close()
            
            # Store document metadata
            self.document_metadata[document_name] = pdf_info
            
            logger.info(f"Extracted {len(all_docs)} document chunks from PDF with {total_pages} pages")
            return all_docs
            
        except Exception as e:
            logger.error(f"Error extracting text from PDF: {str(e)}")
            raise

    def create_vectorstore(self, documents: List[Document]) -> FAISS:
        """Create FAISS vectorstore from documents"""
        try:
            logger.info(f"Creating vectorstore for {len(documents)} documents")
            vectordb = FAISS.from_documents(documents, self.embedding_model)
            logger.info("Vectorstore created successfully")
            return vectordb
        except Exception as e:
            logger.error(f"Error creating vectorstore: {str(e)}")
            raise

    def search_similar_chunks(self, query: str, document_id: str, top_k: int = 5) -> List[Document]:
        """Search for similar chunks using the vectorstore retriever"""
        try:
            if document_id not in self.vectorstores:
                raise ValueError(f"Document {document_id} not found")
            
            logger.info(f"Searching for similar chunks for query: {query[:50]}...")
            vectordb = self.vectorstores[document_id]
            
            # Use similarity search with scores for better results
            docs_with_scores = vectordb.similarity_search_with_score(query, k=top_k)
            
            # Sort by relevance score (lower is better for FAISS)
            docs_with_scores.sort(key=lambda x: x[1])
            
            relevant_docs = [doc for doc, score in docs_with_scores]
            
            logger.info(f"Found {len(relevant_docs)} relevant chunks")
            return relevant_docs
        except Exception as e:
            logger.error(f"Error searching similar chunks: {str(e)}")
            raise

    def format_source_info(self, doc: Document) -> str:
        """Format source information for display"""
        metadata = doc.metadata
        source = metadata.get('source', 'Unknown')
        page_num = metadata.get('page_number', 'Unknown')
        section = metadata.get('section', 'Unknown')
        
        source_info = f"📄 {source} | Page {page_num}"
        if section and section != 'Unknown':
            source_info += f" | Section: {section}"
        
        return source_info

    def generate_answer(self, query: str, relevant_docs: List[Document]) -> str:
        """Generate answer using Gemini with source citations"""
        try:
            logger.info(f"Generating answer for query: {query[:50]}...")
            
            # Create context with source information
            context_parts = []
            for i, doc in enumerate(relevant_docs, 1):
                source_info = self.format_source_info(doc)
                context_part = f"[SOURCE {i}] {source_info}\n{doc.page_content}\n"
                context_parts.append(context_part)
            
            context = "\n".join(context_parts)
            
            prompt = f"""
You are a helpful assistant that answers questions based on PDF documents. 
Use the following context to answer the question. ALWAYS mention the source document name and page number when referencing information.

Context:
{context}

Question: {query}

Instructions:
1. Provide a comprehensive answer based on the context
2. ALWAYS cite your sources by mentioning the document name and page number like: "According to [Document Name], page [X]..."
3. If information comes from multiple sources, cite all relevant sources
4. Format your citations clearly in your response
5. If you cannot find relevant information in the context, state that clearly

Answer:
"""
            
            response = self.model.generate_content(prompt)
            answer = response.text
            
            # Add source information at the end if not already present
            if "page" not in answer.lower() and "source" not in answer.lower():
                sources_summary = "\n\nSources:\n"
                for i, doc in enumerate(relevant_docs, 1):
                    sources_summary += f"• {self.format_source_info(doc)}\n"
                answer += sources_summary
            
            logger.info("Answer generated successfully")
            return answer
        except Exception as e:
            logger.error(f"Error generating answer: {str(e)}")
            raise

    def export_qa_to_pdf(self, filename: str = "qa_export.pdf") -> str:
        """Export Q&A history to PDF"""
        try:
            pdf = FPDF()
            pdf.set_auto_page_break(auto=True, margin=15)
            pdf.add_page()
            pdf.set_font("Arial", size=12)

            now = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
            pdf.cell(200, 10, txt=f"Q&A Session Export - {now}", ln=True, align="C")
            pdf.ln(10)

            for idx, entry in enumerate(self.qa_log, start=1):
                pdf.set_font("Arial", 'B', size=12)
                pdf.multi_cell(0, 10, f"Q{idx}: {entry['question']}")
                pdf.ln(5)
                
                pdf.set_font("Arial", '', size=12)
                pdf.multi_cell(0, 10, f"A{idx}: {entry['answer']}")
                pdf.ln(5)
                
                pdf.set_font("Arial", 'I', size=10)
                pdf.multi_cell(0, 10, f"Sources:")
                for source in entry.get("sources", []):
                    pdf.multi_cell(0, 8, f"• {source}")
                pdf.ln(10)

            output_path = os.path.join(self.config.get("temp_folder", "./temp"), filename)
            pdf.output(output_path)
            logger.info(f"PDF exported as: {output_path}")
            return output_path
        except Exception as e:
            logger.error(f"Error exporting to PDF: {str(e)}")
            raise

    def process_pdf(self, pdf_path: str, document_id: str) -> Dict[str, Any]:
        """Process PDF and create vectorstore"""
        try:
            logger.info(f"Processing PDF: {pdf_path}")
            
            # Extract text
            document_name = os.path.basename(pdf_path)
            documents = self.extract_text_from_pdf(pdf_path, document_name)
            
            if not documents:
                raise ValueError("No text could be extracted from the PDF")
            
            # Create vectorstore
            vectorstore = self.create_vectorstore(documents)
            
            # Store documents and vectorstore
            self.documents[document_id] = documents
            self.vectorstores[document_id] = vectorstore
            
            result = {
                "document_id": document_id,
                "document_name": document_name,
                "status": "success",
                "chunks_count": len(documents),
                "total_pages": self.document_metadata.get(document_name, {}).get("total_pages", "Unknown"),
                "processed_at": datetime.now().isoformat()
            }
            logger.info(f"PDF processed successfully: {result}")
            return result
        except Exception as e:
            logger.error(f"Error processing PDF: {str(e)}")
            raise

    def answer_question(self, document_id: str, question: str) -> Dict[str, Any]:
        """Answer question for a specific document"""
        try:
            logger.info(f"Answering question for document {document_id}: {question[:50]}...")
            
            if document_id not in self.documents:
                raise ValueError(f"Document {document_id} not found. Please upload and process the PDF first.")
            
            # Get relevant documents
            relevant_docs = self.search_similar_chunks(question, document_id)
            
            if not relevant_docs:
                return {
                    "answer": "I couldn't find relevant information in the document to answer your question.",
                    "sources": [],
                    "source_details": [],
                    "answered_at": datetime.now().isoformat()
                }
            
            # Generate answer
            answer = self.generate_answer(question, relevant_docs)
            
            # Prepare detailed source information
            sources = []
            source_details = []
            
            for doc in relevant_docs:
                source_info = self.format_source_info(doc)
                sources.append(source_info)
                
                source_detail = {
                    "pdf_name": doc.metadata.get('source', 'Unknown'),
                    "page_number": doc.metadata.get('page_number', 'Unknown'),
                    "section": doc.metadata.get('section', 'Unknown'),
                    "content_preview": doc.page_content[:200].strip() + ("..." if len(doc.page_content) > 200 else ""),
                    "extraction_method": doc.metadata.get('extraction_method', 'unknown')
                }
                source_details.append(source_detail)
            
            # Add to Q&A log
            self.qa_log.append({
                "question": question,
                "answer": answer,
                "sources": sources,
                "source_details": source_details,
                "timestamp": datetime.now().isoformat()
            })
            
            result = {
                "answer": answer,
                "sources": sources,
                "source_details": source_details,
                "relevant_chunks": [
                    {
                        "content": doc.page_content[:300].strip() + ("..." if len(doc.page_content) > 300 else ""),
                        "metadata": doc.metadata
                    } for doc in relevant_docs
                ],
                "answered_at": datetime.now().isoformat()
            }
            
            logger.info("Question answered successfully")
            return result
        except Exception as e:
            logger.error(f"Error answering question: {str(e)}")
            raise


# Instantiate processor
processor = PdfQAProcessor()

@app.route('/initialize', methods=['POST'])
def initialize():
    try:
        logger.info("Initializing PDF Q&A processor...")
        processor.documents.clear()
        processor.vectorstores.clear()
        processor.qa_log.clear()
        processor.document_metadata.clear()
        return jsonify({
            "status": "success",
            "message": "PDF Q&A processor initialized successfully",
            "initialized_at": datetime.now().isoformat()
        })
    except Exception as e:
        logger.error(f"Error in initialize endpoint: {str(e)}")
        return jsonify({"error": "Failed to initialize", "details": str(e)}), 500


@app.route('/process', methods=['POST'])
def process_pdfs():
    try:
        data = request.get_json()
        if not data or 'PdfPaths' not in data:
            return jsonify({"error": "Missing PdfPaths"}), 400

        pdf_paths = data['PdfPaths']
        if not isinstance(pdf_paths, list) or len(pdf_paths) == 0:
            return jsonify({"error": "PdfPaths must be a non-empty list"}), 400

        logger.info(f"Processing {len(pdf_paths)} PDF files...")
        results = []

        for i, pdf_path in enumerate(pdf_paths):
            try:
                if not os.path.exists(pdf_path):
                    logger.warning(f"PDF file not found: {pdf_path}")
                    results.append({
                        "pdf_path": pdf_path,
                        "status": "error",
                        "error": "File not found"
                    })
                    continue

                document_id = f"doc_{datetime.now().strftime('%Y%m%d_%H%M%S')}_{i}_{abs(hash(pdf_path)) % 10000}"
                result = processor.process_pdf(pdf_path, document_id)
                result["pdf_path"] = pdf_path
                results.append(result)
                logger.info(f"Successfully processed: {pdf_path}")
            except Exception as e:
                logger.error(f"Error processing {pdf_path}: {str(e)}")
                results.append({
                    "pdf_path": pdf_path,
                    "status": "error",
                    "error": str(e)
                })

        successful = len([r for r in results if r.get("status") == "success"])
        failed = len(results) - successful

        return jsonify({
            "status": "completed",
            "total_files": len(pdf_paths),
            "successful": successful,
            "failed": failed,
            "results": results,
            "processed_at": datetime.now().isoformat()
        })
    except Exception as e:
        logger.error(f"Error in process_pdfs endpoint: {str(e)}")
        return jsonify({"error": "Internal server error", "details": str(e)}), 500


@app.route('/ask', methods=['POST'])
def ask_question_multiple():
    try:
        data = request.get_json()
        if not data or 'question' not in data:
            return jsonify({"error": "Missing question"}), 400

        question = data['question']
        if not question.strip():
            return jsonify({"error": "Question cannot be empty"}), 400

        if not processor.documents:
            return jsonify({"error": "No documents found. Please upload and process PDFs first."}), 400

        logger.info(f"Answering question across {len(processor.documents)} documents: {question[:50]}...")
        all_relevant_docs = []
        all_sources = []
        all_source_details = []

        # Search across all documents
        for document_id in processor.documents.keys():
            try:
                relevant_docs = processor.search_similar_chunks(question, document_id, top_k=3)
                all_relevant_docs.extend(relevant_docs)
                
                for doc in relevant_docs:
                    source_info = processor.format_source_info(doc)
                    all_sources.append(source_info)
                    
                    source_detail = {
                        "pdf_name": doc.metadata.get('source', 'Unknown'),
                        "page_number": doc.metadata.get('page_number', 'Unknown'),
                        "section": doc.metadata.get('section', 'Unknown'),
                        "content_preview": doc.page_content[:200].strip() + ("..." if len(doc.page_content) > 200 else ""),
                        "extraction_method": doc.metadata.get('extraction_method', 'unknown')
                    }
                    all_source_details.append(source_detail)
                    
            except Exception as e:
                logger.warning(f"Error searching in document {document_id}: {str(e)}")

        if not all_relevant_docs:
            return jsonify({
                "answer": "I couldn't find relevant information in any of the processed documents.",
                "sources": [],
                "source_details": [],
                "documents_searched": len(processor.documents),
                "answered_at": datetime.now().isoformat()
            })

        # Generate answer using top 5 most relevant chunks
        answer = processor.generate_answer(question, all_relevant_docs[:5])
        
        # Add to Q&A log
        processor.qa_log.append({
            "question": question,
            "answer": answer,
            "sources": all_sources[:5],
            "source_details": all_source_details[:5],
            "timestamp": datetime.now().isoformat()
        })

        return jsonify({
            "answer": answer,
            "sources": all_sources[:5],
            "source_details": all_source_details[:5],
            "relevant_chunks": [
                {
                    "content": doc.page_content[:300].strip() + ("..." if len(doc.page_content) > 300 else ""),
                    "metadata": doc.metadata
                } for doc in all_relevant_docs[:5]
            ],
            "documents_searched": len(processor.documents),
            "answered_at": datetime.now().isoformat()
        })
    except Exception as e:
        logger.error(f"Error in ask_question endpoint: {str(e)}")
        return jsonify({"error": "Internal server error", "details": str(e)}), 500


@app.route('/ask/<document_id>', methods=['POST'])
def ask_question_single(document_id):
    try:
        data = request.get_json()
        if not data or 'question' not in data:
            return jsonify({"error": "Missing question"}), 400

        question = data['question']
        if not question.strip():
            return jsonify({"error": "Question cannot be empty"}), 400

        if document_id not in processor.documents:
            return jsonify({"error": f"Document {document_id} not found. Please upload and process the PDF first."}), 404

        relevant_docs = processor.search_similar_chunks(question, document_id)
        if not relevant_docs:
            return jsonify({
                "answer": "I couldn't find relevant information in the document to answer your question.",
                "sources": [],
                "source_details": [],
                "answered_at": datetime.now().isoformat()
            })

        answer = processor.generate_answer(question, relevant_docs)

        sources = []
        source_details = []
        for doc in relevant_docs:
            source_info = processor.format_source_info(doc)
            sources.append(source_info)
            
            source_detail = {
                "pdf_name": doc.metadata.get('source', 'Unknown'),
                "page_number": doc.metadata.get('page_number', 'Unknown'),
                "section": doc.metadata.get('section', 'Unknown'),
                "content_preview": doc.page_content[:200].strip() + ("..." if len(doc.page_content) > 200 else ""),
                "extraction_method": doc.metadata.get('extraction_method', 'unknown')
            }
            source_details.append(source_detail)

        processor.qa_log.append({
            "question": question,
            "answer": answer,
            "sources": sources,
            "source_details": source_details,
            "timestamp": datetime.now().isoformat()
        })

        return jsonify({
            "answer": answer,
            "sources": sources,
            "source_details": source_details,
            "relevant_chunks": [
                {
                    "content": doc.page_content[:300].strip() + ("..." if len(doc.page_content) > 300 else ""),
                    "metadata": doc.metadata
                } for doc in relevant_docs
            ],
            "answered_at": datetime.now().isoformat()
        })

    except Exception as e:
        logger.error(f"Error in ask_question_single endpoint: {str(e)}")
        return jsonify({"error": "Internal server error", "details": str(e)}), 500


@app.route('/export', methods=['POST'])
def export_qa():
    try:
        pdf_path = processor.export_qa_to_pdf()
        return send_file(
            pdf_path,
            mimetype='application/pdf',
            as_attachment=True,
            download_name='qa_export.pdf'
        )
    except Exception as e:
        logger.error(f"Error in export_qa endpoint: {str(e)}")
        return jsonify({"error": "Internal server error", "details": str(e)}), 500


@app.route('/export/json', methods=['POST'])
def export_qa_json():
    """Export Q&A history as JSON"""
    try:
        data = request.get_json()
        export_data = {
            "exported_at": datetime.now().isoformat(),
            "total_documents": len(processor.documents),
            "document_metadata": processor.document_metadata,
            "qa_history": processor.qa_log,
            "documents": []
        }
        
        for doc_id in processor.documents:
            doc_info = {
                "document_id": doc_id,
                "chunks_count": len(processor.documents[doc_id]),
                "status": "processed"
            }
            
            # Add document name and page info
            if processor.documents[doc_id]:
                first_doc = processor.documents[doc_id][0]
                doc_info["document_name"] = first_doc.metadata.get("source", "Unknown")
                doc_info["total_pages"] = first_doc.metadata.get("total_pages", "Unknown")
            
            if data and data.get('include_content'):
                doc_info["content"] = [
                    {
                        "page_content": doc.page_content,
                        "metadata": doc.metadata
                    } for doc in processor.documents[doc_id]
                ]
            export_data["documents"].append(doc_info)
            
        return jsonify(export_data)
    except Exception as e:
        logger.error(f"Error in export_qa_json endpoint: {str(e)}")
        return jsonify({"error": "Internal server error", "details": str(e)}), 500


@app.route('/clear', methods=['POST'])
def clear_documents():
    try:
        processor.documents.clear()        # ✅ Clears processed text
        processor.vectorstores.clear()     # ✅ Clears vector indexes
        processor.qa_log.clear()          # ✅ Clears conversation history
        processor.document_metadata.clear() # ✅ Clears metadata
        
        return jsonify({
            "status": "success",
            "message": "All documents and Q&A history cleared successfully",
            "cleared_at": datetime.now().isoformat()
        })
    except Exception as e:
        logger.error(f"Error in clear_documents endpoint: {str(e)}")
        return jsonify({"error": "Internal server error", "details": str(e)}), 500


@app.route('/upload', methods=['POST'])
def upload_pdf():
    try:
        if 'file' not in request.files:
            return jsonify({"error": "No file provided"}), 400

        file = request.files['file']
        if file.filename == '':
            return jsonify({"error": "No file selected"}), 400

        if not file.filename.lower().endswith('.pdf'):
            return jsonify({"error": "Only PDF files are supported"}), 400

        document_id = f"doc_{datetime.now().strftime('%Y%m%d_%H%M%S')}_{abs(hash(file.filename)) % 10000}"
        with tempfile.NamedTemporaryFile(delete=False, suffix='.pdf') as tmp_file:
            file.save(tmp_file.name)
            result = processor.process_pdf(tmp_file.name, document_id)
            os.unlink(tmp_file.name)
            return jsonify(result)
    except Exception as e:
        logger.error(f"Error in upload endpoint: {str(e)}")
        return jsonify({"error": "Internal server error", "details": str(e)}), 500


@app.route('/documents', methods=['GET'])
def list_documents():
    try:
        documents = []
        for doc_id in processor.documents:
            doc_info = {
                "document_id": doc_id,
                "chunks_count": len(processor.documents[doc_id]),
                "status": "processed"
            }
            
            # Add document name and page info
            if processor.documents[doc_id]:
                first_doc = processor.documents[doc_id][0]
                doc_info["document_name"] = first_doc.metadata.get("source", "Unknown")
                doc_info["total_pages"] = first_doc.metadata.get("total_pages", "Unknown")
                doc_info["processed_at"] = first_doc.metadata.get("processed_at", "Unknown")
            
            documents.append(doc_info)

        return jsonify({
            "documents": documents,
            "total_count": len(documents),
            "qa_history_count": len(processor.qa_log),
            "document_metadata": processor.document_metadata
        })
    except Exception as e:
        logger.error(f"Error in documents endpoint: {str(e)}")
        return jsonify({"error": "Internal server error", "details": str(e)}), 500


@app.route('/documents/<document_id>', methods=['DELETE'])
def delete_document(document_id):
    try:
        if document_id not in processor.documents:
            return jsonify({"error": "Document not found"}), 404

        # Get document name before deleting
        doc_name = "Unknown"
        if processor.documents[document_id]:
            doc_name = processor.documents[document_id][0].metadata.get("source", "Unknown")

        del processor.documents[document_id]
        del processor.vectorstores[document_id]
        
        # Also remove from metadata if exists
        if doc_name in processor.document_metadata:
            del processor.document_metadata[doc_name]

        return jsonify({
            "message": "Document deleted successfully",
            "document_id": document_id,
            "document_name": doc_name
        })
    except Exception as e:
        logger.error(f"Error in delete endpoint: {str(e)}")
        return jsonify({"error": "Internal server error", "details": str(e)}), 500


@app.route('/health', methods=['GET'])
def health_check():
    return jsonify({
        "status": "healthy",
        "timestamp": datetime.now().isoformat(),
        "version": "1.0.0",
        "documents_loaded": len(processor.documents),
        "vectorstores_loaded": len(processor.vectorstores)
    })


@app.route('/test', methods=['GET', 'POST'])
def test_endpoint():
    try:
        return jsonify({
            "status": "success",
            "message": "Test endpoint working",
            "timestamp": datetime.now().isoformat(),
            "method": request.method,
            "documents_count": len(processor.documents),
            "vectorstores_count": len(processor.vectorstores),
            "qa_history_count": len(processor.qa_log)
        })
    except Exception as e:
        logger.error(f"Error in test endpoint: {str(e)}")
        return jsonify({"error": "Internal server error", "details": str(e)}), 500


@app.route('/test-gemini', methods=['GET'])
def test_gemini():
    try:
        # Check if Gemini API is properly configured
        if not hasattr(processor, 'model'):
            return jsonify({
                "status": "error",
                "error": "Gemini model not initialized",
                "hint": "Check if your API key is set correctly in config.json"
            }), 500
            
        # Test if Gemini is working
        test_prompt = "Say 'Hello, I am working!' if you receive this message."
        response = processor.model.generate_content(test_prompt)
        
        return jsonify({
            "status": "success",
            "message": "Gemini API is connected and working",
            "gemini_response": response.text,
            "model_name": processor.config.get("model_name", "Unknown"),
            "timestamp": datetime.now().isoformat()
        })
    except Exception as e:
        logger.error(f"Gemini test failed: {str(e)}")
        error_message = str(e)
        
        # Provide helpful error messages
        if "API_KEY_INVALID" in error_message:
            hint = "Your Gemini API key is invalid. Please check your config.json file."
        elif "Gemini API key not found" in error_message:
            hint = "No API key found. Please add your Gemini API key to config.json."
        else:
            hint = "Check if your API key is set correctly in config.json and you have access to the Gemini API."
            
        return jsonify({
            "status": "error",
            "error": "Gemini API connection failed",
            "details": error_message,
            "hint": hint
        }), 500


@app.errorhandler(404)
def not_found(error):
    return jsonify({"error": "Endpoint not found"}), 404


@app.errorhandler(500)
def internal_error(error):
    return jsonify({"error": "Internal server error"}), 500


if __name__ == '__main__':
    # Create necessary directories
    os.makedirs('./uploads', exist_ok=True)
    os.makedirs('./temp', exist_ok=True)
    
    # Log startup information
    logger.info("Starting PDF Q&A API server...")
    logger.info(f"Config loaded: {processor.config}")
    logger.info(f"Gemini model: {processor.config.get('model_name', 'Unknown')}")
    logger.info(f"Embedding model: {processor.config.get('embedding_model', 'Unknown')}")
    
    # Start the Flask app
    app.run(host='0.0.0.0', port=5000, debug=True)