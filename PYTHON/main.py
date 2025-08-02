# 🚀 STEP 1: Install Dependencies

# 📤 STEP 2: Upload PDFs
from google.colab import files
import os, tempfile

print("📤 Please upload one or more PDF files (you can select multiple files at once)...")
uploaded_files = files.upload()

print(f"\n✅ Uploaded {len(uploaded_files)} files:")
for name in uploaded_files:
    print(" -", name)

# 🧾 STEP 3: Imports
import fitz  # PyMuPDF
import pytesseract
from pdf2image import convert_from_path
from langchain.docstore.document import Document
from langchain.text_splitter import RecursiveCharacterTextSplitter
from langchain.vectorstores import FAISS
from langchain.embeddings import HuggingFaceEmbeddings
import google.generativeai as genai
from fpdf import FPDF
from datetime import datetime
import cv2
import numpy as np
from PIL import Image

# 🔧 STEP 3.1: Image Preprocessing for OCR
def preprocess_image_for_ocr(pil_image):
    img = np.array(pil_image.convert('L'))  # Grayscale
    img = cv2.adaptiveThreshold(img, 255, cv2.ADAPTIVE_THRESH_MEAN_C,
                                cv2.THRESH_BINARY, 11, 12)  # Binarize
    img = cv2.medianBlur(img, 3)  # Denoise
    return Image.fromarray(img)

# 🔐 STEP 4: Configure Gemini
GEMINI_API_KEY = "AIzaSyCvvfiWptX8H_oqXZo3DTsPvNvUtleMkAM"  # 🔑 Replace this with your API key
genai.configure(api_key=GEMINI_API_KEY)
model = genai.GenerativeModel("models/gemini-1.5-flash")

# 📄 STEP 5: Extract Text from PDFs (OCR + preprocessing)
all_docs = []

for name in uploaded_files:
    temp_path = os.path.join(tempfile.gettempdir(), name)
    with open(temp_path, "wb") as f:
        f.write(uploaded_files[name])

    try:
        doc = fitz.open(temp_path)
        for page_num, page in enumerate(doc, start=1):
            text = page.get_text()

            # 🧐 Use OCR if no text found
            if not text.strip():
                print(f"🧐 Using OCR for Page {page_num} in {name}")
                images = convert_from_path(temp_path, first_page=page_num, last_page=page_num)
                processed_image = preprocess_image_for_ocr(images[0])
                text = pytesseract.image_to_string(processed_image, config='--psm 6')

                # ✅ Check OCR confidence
                ocr_data = pytesseract.image_to_data(processed_image, output_type=pytesseract.Output.DICT)
                confidences = []
                for conf in ocr_data["conf"]:
                    try:
                        confidences.append(int(conf))
                    except (ValueError, TypeError):
                        continue
                avg_conf = sum(confidences) / len(confidences) if confidences else 0
                if avg_conf < 50:
                    print(f"⚠ OCR confidence low ({avg_conf:.1f}) on Page {page_num} of {name}")

            # Extract section title
            section_title = ""
            for line in text.split("\n"):
                if line.strip().isupper() and len(line.strip()) > 5:
                    section_title = line.strip()
                    break

            # Split text into chunks
            splitter = RecursiveCharacterTextSplitter(chunk_size=800, chunk_overlap=200)
            chunks = splitter.split_text(text)

            for chunk in chunks:
                metadata = {
                    "source": name,
                    "page_number": page_num,
                    "section": section_title or "Unknown"
                }
                all_docs.append(Document(page_content=chunk, metadata=metadata))

    except Exception as e:
        print(f"❌ Failed to process {name}: {e}")

# 🧠 STEP 6: Embedding and Retriever Setup
embedding_model = HuggingFaceEmbeddings(model_name="all-MiniLM-L6-v2")
vectordb = FAISS.from_documents(all_docs, embedding_model)
retriever = vectordb.as_retriever(search_type="similarity", search_kwargs={"k": 3})

# 📝 STEP 7: Q&A History Log
qa_log = []

# 🗂 STEP 8: Export Q&A to PDF
def export_qa_to_pdf(log, filename="qa_export.pdf"):
    pdf = FPDF()
    pdf.set_auto_page_break(auto=True, margin=15)
    pdf.add_page()
    pdf.set_font("Arial", size=12)

    now = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    pdf.cell(200, 10, txt=f"Q&A Session Export - {now}", ln=True, align="C")
    pdf.ln(10)

    for idx, entry in enumerate(log, start=1):
        pdf.set_font("Arial", 'B', size=12)
        pdf.multi_cell(0, 10, f"Q{idx}: {entry['question']}")
        pdf.set_font("Arial", '', size=12)
        pdf.multi_cell(0, 10, f"A{idx}: {entry['answer']}")
        pdf.set_font("Arial", 'I', size=10)
        pdf.multi_cell(0, 10, f"Sources:\n" + "\n".join(entry["sources"]))
        pdf.ln(10)

    pdf.output(filename)
    print(f"\n📄 PDF exported as: {filename}")

# 💬 STEP 9: Interactive Q&A Loop
while True:
    question = input("\n💬 Ask your question (or type 'exit' to quit): ")
    if question.lower() == "exit":
        print("👋 Session ended.")
        export_qa_to_pdf(qa_log)
        break

    relevant_docs = retriever.get_relevant_documents(question)
    context = "\n\n".join(doc.page_content for doc in relevant_docs)

    prompt = f"""
Use the following context to answer the question.

Context:
{context}

Question: {question}
Answer:
"""

    try:
        response = model.generate_content(prompt)
        print("\n📘 Answer:")
        print(response.text)

        qa_log.append({
            "question": question,
            "answer": response.text,
            "sources": [
                f"{doc.metadata['source']} - Page {doc.metadata['page_number']} - Section: {doc.metadata['section']}"
                for doc in relevant_docs
            ]
        })

    except Exception as e:
        print(f"❌ Gemini error: {e}")
        continue

    print("\n📄 Sources:")
    for doc in relevant_docs:
        print(f"📘 File: {doc.metadata['source']} | 📄 Page: {doc.metadata['page_number']} | 📑 Section: {doc.metadata['section']}")
        print(doc.page_content[:300].strip() + "...\n—")

# ✅ STEP 10: Trigger Download of PDF
from google.colab import files
files.download("qa_export.pdf")