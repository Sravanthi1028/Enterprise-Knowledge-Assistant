# Enterprise Knowledge Assistant

An AI-powered Enterprise Knowledge Assistant that transforms unstructured business documents into searchable knowledge using Semantic Search and Retrieval-Augmented Generation (RAG).

## Project Overview

The Enterprise Knowledge Assistant enables organizations to upload, process, search, and retrieve information from business documents. It combines keyword search, semantic search, and AI-powered question answering to provide relevant, context-aware responses with source references.

## Key Features

- **Document Management:** Upload and manage PDF, DOCX, and TXT documents.
- **Text Extraction:** Extract text and divide documents into manageable chunks.
- **Keyword Search:** Find information using keyword-based retrieval.
- **Semantic Search:** Retrieve relevant content using embeddings.
- **Hybrid Search:** Combine keyword and semantic search.
- **RAG-Based Question Answering:** Generate answers using retrieved document context.
- **Source Citations:** Display relevant document sources.
- **Authentication:** JWT-based authentication.
- **Role-Based Access Control:** Separate Admin and Employee permissions.
- **Conversation History:** Create and manage conversations.

## Technology Stack

| Component | Technology |
|---|---|
| Backend | C#, ASP.NET Core Web API |
| Frontend | Python, Streamlit |
| Database | MySQL |
| AI Integration | Google Gemini |
| Architecture | Retrieval-Augmented Generation (RAG) |
| Authentication | JWT |
| Document Formats | PDF, DOCX, TXT |

## Project Structure

```text
Enterprise-Knowledge-Assistant/
│
├── backend/
│   └── EnterpriseKnowledge.API/
│       ├── Controllers/
│       ├── Data/
│       ├── DTOs/
│       ├── Migrations/
│       ├── Models/
│       ├── Properties/
│       ├── Services/
│       ├── Program.cs
│       └── EnterpriseKnowledge.API.csproj
│
├── frontend/
│   ├── app.py
│   └── requirements.txt
│
├── demo/
│
├── README.md
└── .gitignore
```

## Installation and Setup

### Prerequisites

- .NET SDK
- Python
- MySQL Server
- Google Gemini API key

### 1. Clone the Repository

```bash
git clone YOUR_GITHUB_REPOSITORY_URL
```

### 2. Backend Setup

Navigate to the backend project directory:

```bash
cd backend/EnterpriseKnowledge.API
```

Restore dependencies:

```bash
dotnet restore
```

Run database migrations:

```bash
dotnet ef database update
```

Start the backend:

```bash
dotnet run
```

Configure your MySQL connection securely before running the application.

### 3. Frontend Setup

Navigate to the frontend directory:

```bash
cd frontend
```

Install dependencies:

```bash
pip install -r requirements.txt
```

Start Streamlit:

```bash
streamlit run app.py
```

## Environment Configuration

Set your Gemini API key using an environment variable:

```powershell
$env:GEMINI_API_KEY="YOUR_GEMINI_API_KEY"
```

Never commit API keys, database passwords, JWT secrets, or private configuration files.

## Project Demonstration

The project demonstration video will be added here.

## Future Enhancements

- Advanced document retrieval and ranking.
- Improved AI response generation.
- Scalable document processing.
- Enhanced monitoring and deployment.

## Author

**Enterprise Knowledge Assistant**

Developed as an AI-powered enterprise knowledge management project.
