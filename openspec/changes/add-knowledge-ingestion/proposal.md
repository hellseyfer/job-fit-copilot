# Proposal

## Why

Fit scoring (stage 3) needs evidence from the candidate: which parts of the CV and projects support
each requirement. Before evidence can be retrieved, the candidate's documents must be split into
citable chunks, embedded and stored in pgvector. This stage adds that ingestion, without retrieval yet.

## What Changes

- Add `PUT /api/knowledge/documents/{id}`: receives a CV or project as plain text / Markdown
  (`kind`, `title`, `content`), splits it into chunks that keep their section, embeds every chunk and
  stores the document and its chunks in Postgres + pgvector. Re-sending a document replaces its
  chunks; re-sending identical content does not call the embedding model again.
- Add `GET /api/knowledge/documents` (list), `GET /api/knowledge/documents/{id}/chunks` (inspect the
  chunks, without vectors) and `DELETE /api/knowledge/documents/{id}`.
- Generate embeddings behind the `IEmbeddingGenerator` abstraction of `Microsoft.Extensions.AI`;
  OpenAI by default and Ollama as the local alternative, selected by the same `Llm:Provider`
  configuration as the chat model.
- Validate input (kind, id format, empty or oversized content) before calling the model, and map
  embedding failures to a clear error without changing what is stored.
- Trace each embedding call with OpenTelemetry (model, token usage) in the Aspire dashboard.

Out of scope: PDF/DOCX upload and file parsing, similarity search and evidence retrieval (stage 3),
fit scoring, the workflow graph, PII redaction and prompt-injection defenses for stored content
(stage 7), multiple users, authentication and authorization, document versioning or history,
schema migration tooling.

## Capabilities

### New Capabilities
- `knowledge-ingestion`: turning a candidate's CV and project descriptions into stored, embedded,
  section-aware chunks that can later be retrieved as evidence.

### Modified Capabilities
<!-- none -->

## Impact

- New feature folder `Knowledge/` in `JobFit.Api` with the endpoints, chunker, ingestion service and
  Postgres store.
- AppHost declares an `embeddings` model resource next to `chat` for both providers
  (OpenAI `text-embedding-3-small`, Ollama `nomic-embed-text`).
- Database: `vector` extension plus `knowledge_documents` and `knowledge_chunks` tables with an HNSW
  index, created by the API on first use.
- Dependencies: `Pgvector` (Npgsql type mapping); `Testcontainers.PostgreSql` for integration tests.
- Tests use a fake `IEmbeddingGenerator`, so CI needs no model credentials; persistence tests need
  Docker, like the existing integration tests.
