# 📄 PDF Q&A System

A full-stack **PDF Question-Answering (Q&A) system** that allows users to upload PDF files and ask questions based on their content.  
The backend is powered by **.NET Core** for file handling and API management, while **LangChain with Python** handles AI-based embedding, retrieval, and Q&A.

---

## 🚀 Features

- **PDF Upload & Management**  
  Users can upload multiple PDF files for processing.
  
- **AI-Powered Question Answering**  
  Uses **LangChain** and vector embeddings to answer questions from PDF content.
  
- **.NET Core Backend**  
  - Handles file uploads, API requests, and integration with the Python service.
  - Includes folder structure for `Controllers`, `Services`, and `Helpers`.
  
- **Python AI Service**  
  - Extracts text, generates embeddings, and performs Q&A.
  - Uses **FAISS / Chroma** for vector search.
  - Can integrate with **OpenAI/Gemini** or other LLMs for response generation.

---

## 🏗️ Project Structure
