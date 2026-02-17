# Company Knowledge Assistant

A production-ready RAG (Retrieval-Augmented Generation) system for intelligent querying of company documents using AI. Built with ASP.NET Core 8, Angular 21, and Ollama.

## Features

- **Document Management**: Upload and process PDF, DOCX, TXT, and MD files
- **AI-Powered Q&A**: Ask questions and get intelligent responses with source citations
- **Real-time Chat**: SignalR integration for instant message delivery
- **Vector Search**: Semantic similarity search using embeddings
- **Multi-tenant**: Organize documents and chats into separate areas
- **Modern UI**: Responsive Bootstrap 5 interface

## Tech Stack

### Backend
- ASP.NET Core 8
- Entity Framework Core
- SQLite database
- Ollama (llama3.1:8b for LLM, nomic-embed-text for embeddings)
- SignalR for real-time communication
- Serilog for structured logging

### Frontend
- Angular 21
- Bootstrap 5
- RxJS
- SignalR client

### Infrastructure
- Docker & Docker Compose
- Multi-stage Dockerfile for optimized builds

## Prerequisites

- .NET 8 SDK
- Node.js (v18+) and npm
- Docker Desktop (for containerized deployment)
- Ollama CLI (for local development)

## Setup

## Local Ports

- Frontend (Angular dev server): http://localhost:4200
- Backend API (HTTP): http://localhost:5000
- Backend API (HTTPS): https://localhost:7181
- SignalR Hub: http://localhost:5000/chatHub
- Ollama: http://localhost:11434

### 1. Clone Repository

```bash
git clone <repository-url>
cd AIRagSystem
```

### 2. Backend Setup

```bash
cd CompanyKnowledgeAssistant.API
dotnet restore
dotnet ef database update --project ../CompanyKnowledgeAssistant.Infrastructure --startup-project .
dotnet run
```

API will be available at http://localhost:5000

### 3. Frontend Setup

```bash
cd company-knowledge-assistant-ui
npm install
ng serve
```

Frontend will be available at http://localhost:4200

### 4. Ollama Setup (Local Development)

```bash
# Install Ollama from https://ollama.ai
ollama pull llama3.1:8b
ollama pull nomic-embed-text
ollama serve
```

## Running with Docker

### Quick Start

```bash
# Build and start all services
docker-compose up -d

# Pull required Ollama models
docker exec -it ollama ollama pull llama3.1:8b
docker exec -it ollama ollama pull nomic-embed-text
```

Services:
- API: http://localhost:5000
- Ollama: http://localhost:11434

### Stop Services

```bash
docker-compose down
```

## Project Structure

```
AIRagSystem/
├── CompanyKnowledgeAssistant.API/          # Web API project
│   ├── Controllers/                        # REST API endpoints
│   ├── Hubs/                               # SignalR hubs
│   └── Program.cs                          # Application entry point
├── CompanyKnowledgeAssistant.Core/         # Domain entities
│   └── Entities/                           # Database models
├── CompanyKnowledgeAssistant.Infrastructure/
│   ├── Data/                               # EF Core DbContext
│   ├── Services/                           # Business logic
│   │   ├── DocumentProcessorService.cs     # Document parsing & chunking
│   │   ├── EmbeddingService.cs             # Vector embeddings
│   │   ├── VectorStoreService.cs           # Similarity search
│   │   ├── OllamaLlmService.cs             # LLM integration
│   │   └── HtmlSanitizerService.cs         # XSS protection
│   └── Migrations/                         # Database migrations
└── company-knowledge-assistant-ui/         # Angular frontend
    └── src/app/
        ├── core/services/                  # API & SignalR services
        ├── features/                       # Feature modules
        └── shared/                         # Shared components
```

## Usage

### 1. Create an Area

Areas are logical groupings for related documents and chats (e.g., "HR Policies", "IT Documentation").

### 2. Upload Documents

- Supported formats: PDF, DOCX, TXT, MD
- Documents are automatically processed: text extraction → chunking → embedding generation
- Processing status is tracked in real-time

### 3. Start a Chat

Create a chat within an area to ask questions about uploaded documents.

### 4. Ask Questions

- Type your question and press Enter
- AI generates responses based on document content
- Source citations show which documents were used
- Responses are formatted with HTML for better readability

## RAG Pipeline

1. **Text Extraction**: Extract text from uploaded documents (PDF, DOCX, TXT, MD)
2. **Chunking**: Split text into 600-800 character chunks with 100-character overlap
3. **Embedding Generation**: Convert chunks to 768-dimensional vectors using nomic-embed-text
4. **Storage**: Store chunks and embeddings in SQLite database
5. **Query Processing**:
   - Generate embedding for user question
   - Perform cosine similarity search
   - Retrieve top 7 most relevant chunks
   - Build context from retrieved chunks
   - Generate response with llama3.1:8b
   - Sanitize HTML output

## Configuration

### Backend (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=app.db"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434"
  }
}
```

### Frontend (`src/app/core/config/api.config.ts`)

```typescript
export const API_BASE_URL = 'http://localhost:5000/api';
export const SIGNALR_HUB_URL = 'http://localhost:5000/chatHub';
```

## API Endpoints

### Areas
- `GET /api/areas` - List all areas
- `POST /api/areas` - Create area
- `GET /api/areas/{id}` - Get area details
- `PUT /api/areas/{id}` - Update area
- `DELETE /api/areas/{id}` - Delete area

### Documents
- `GET /api/documents?areaId={areaId}` - List documents
- `POST /api/documents/upload?areaId={areaId}` - Upload document
- `GET /api/documents/{id}` - Get document details

### Chats
- `GET /api/chats?areaId={areaId}` - List chats
- `POST /api/chats` - Create chat
- `GET /api/chats/{id}` - Get chat details
- `PUT /api/chats/{id}` - Update chat
- `DELETE /api/chats/{id}` - Delete chat

### Messages
- `GET /api/messages?chatId={chatId}` - List messages
- `POST /api/messages` - Send message (triggers RAG query)

## Development

### Running Tests

```bash
# Backend tests (when implemented)
dotnet test

# Frontend tests
cd company-knowledge-assistant-ui
ng test
```

### Database Migrations

```bash
# Create new migration
dotnet ef migrations add MigrationName --project CompanyKnowledgeAssistant.Infrastructure --startup-project CompanyKnowledgeAssistant.API

# Apply migrations
dotnet ef database update --project CompanyKnowledgeAssistant.Infrastructure --startup-project CompanyKnowledgeAssistant.API
```

## Security Considerations

- **HTML Sanitization**: All LLM-generated HTML is sanitized to prevent XSS attacks
- **CORS**: Configured for localhost development only (update for production)
- **File Upload**: Only allowed extensions are accepted (.pdf, .docx, .txt, .md)
- **Input Validation**: All user inputs are validated on both client and server

## Performance

- **Parallel Embedding Generation**: Process multiple chunks concurrently
- **In-Memory Vector Search**: Fast for <10K chunks (consider vector DB for larger datasets)
- **Background Processing**: Document processing runs asynchronously
- **SignalR**: Efficient real-time updates without polling

## Troubleshooting

### Ollama Connection Issues

```bash
# Verify Ollama is running
curl http://localhost:11434/api/tags

# Check Docker container logs
docker logs ollama
```

### Database Issues

```bash
# Reset database
rm app.db
dotnet ef database update
```

### Frontend Build Issues

```bash
# Clear node_modules and reinstall
rm -rf node_modules package-lock.json
npm install
```

## License

MIT License - feel free to use this project for your own purposes.

## Contributing

Contributions are welcome! Please open an issue or submit a pull request.

## Roadmap

- [ ] Add OCR support for scanned PDFs (Tesseract integration)
- [ ] Implement user authentication and authorization
- [ ] Add support for more document formats (Excel, PowerPoint)
- [ ] Migrate to vector database (PostgreSQL with pgvector or Qdrant)
- [ ] Add conversation memory for multi-turn dialogs
- [ ] Implement document versioning
- [ ] Add analytics dashboard
- [ ] Deploy to cloud (Azure/AWS)

## Architecture

- **API Controllers**: Areas, Documents, Chats, Messages
- **Services**: Document processing, embeddings, vector search, LLM generation
- **Database**: SQLite with vector embeddings
- **Real-time**: SignalR for live chat updates