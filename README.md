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

PdfQAApp/ # .NET Core backend
├── Controllers/ # API Controllers
├── Services/ # Service layer for handling business logic
├── Helpers/ # Utility classes
├── uploads/ # Temporary PDF uploads (ignored by Git)
├── Program.cs
└── PdfQAApp.csproj

PYTHON/ # Python AI service
├── apiBridge.py # Flask/FastAPI bridge for Q&A
├── main.py # Main pipeline with LangChain
├── uploads/ # Temporary storage for processed PDFs
├── config.json # API keys & model config (ignored by Git)
└── venv/ # Python virtual environment



---




## ⚡ Tech Stack

**Backend (API & File Management)**  
- ASP.NET Core (C# .NET 6/7)  
- REST API architecture  

**AI Service (Q&A & Embeddings)**  
- Python 3.10+  
- LangChain  
- FAISS / Chroma for vector search  
- OpenAI / Gemini LLM for Q&A



---




## ⚙️ Setup Instructions

### **Backend (.NET Core)**

1. Navigate to `PdfQAApp/`
2. Restore packages:
   ```bash
   dotnet restore
3. Run the API
   ```bash
   dotnet run



   

## 📌 Future Enhancements
React-based frontend for interactive Q&A.

Multi-PDF querying and comparison.

User authentication & session-based PDF history.

Dockerized deployment for easier hosting.

👨‍💻 Author
Developed by Nikshay – A hybrid AI + .NET solution for smart document Q&A.

yaml
Copy
Edit

---

Let me know if you want:
- Badges (like `.NET`, `Python`, `OpenAI`, etc.)  
- A version with screenshots or demo GIF  
- A public link preview to test the GitHub rendering  

Happy building 🚀
