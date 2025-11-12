# Semantic Search with Ollama + Qdrant (GitHub-ready)

This repository demonstrates a minimal semantic-search stack:
- Ollama (local models) for embeddings (optional service)
- Qdrant as vector DB (Docker Compose)
- Python ingestion pipeline with PDF/HTML loaders, chunking, and upsert to Qdrant
- Reranking plugin (lexical + vector hybrid)
- .NET 7 Web API that embeds queries and queries Qdrant
- CI workflow for building .NET and linting Python

## Quick start (local)
1. Start Qdrant:
```bash
docker compose up -d qdrant
```

2. (Optional) Run Ollama on host or as service. The compose includes an optional `ollama` section (commented).
   If you run Ollama locally, ensure it listens on `http://localhost:11434`.

3. Ingest documents (PDF/HTML/plain text)
```bash
cd python
python -m venv venv
# Unix
source venv/bin/activate
# Windows
# venv\Scripts\activate
pip install -r requirements.txt
# Put your documents under python/data/ (pdf / html / txt)
python ingest.py --data-dir data --batch-size 16
```

4. Run the .NET Web API (local)
```bash
cd dotnet/SemanticSearchApi
dotnet run
```

5. Query (example)
```bash
POST http://localhost:5000/api/search/query
Body: { "query": "How do I configure IIS?", "topK": 5, "rerank": true }
```

## What I added beyond the starter
- `python/data_loader.py` — loaders for PDF, HTML, and TXT files.
- Chunking utility (`chunk_text`) with configurable chunk_size and overlap.
- A lightweight reranker (`python/rerank.py`) that combines Qdrant vector score with a lexical similarity score (SequenceMatcher) to refine returned results.
- Example `.env.example` and enhanced README.

## Notes
- For production: use secure service discovery, authentication, proper error handling, monitoring, batching/backpressure on Ollama, and connection pooling for Qdrant.
- This repo assumes Ollama embeddings endpoint shape that returns either `{"embeddings":[...]}` or a raw list.
