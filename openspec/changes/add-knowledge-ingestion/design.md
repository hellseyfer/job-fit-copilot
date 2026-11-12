# Design

## Context

Stage 1 added the LLM resource (`chat`) to the AppHost and the `IChatClient` pipeline in the API,
OpenAI by default and Ollama as the local alternative. Postgres runs the `pgvector/pgvector:pg17`
image and the API already registers an `NpgsqlDataSource` (`jobfit`), but nothing uses it yet.
`JobFit.Api.Tests` runs against an unreachable database on purpose; happy paths that need Postgres
belong in `JobFit.IntegrationTests` (AGENTS.md).

## Goals / Non-Goals

**Goals:**
- Store chunks in a shape that stage 3 can search with a single SQL query (cosine distance, filter by
  document kind) and cite (document, section, text).
- Same provider-agnostic pattern as stage 1: the ingestion code only sees `IEmbeddingGenerator`.
- Atomic replacement: a failed ingestion never leaves a document half-replaced.

**Non-Goals:**
- Search endpoints or relevance tuning (stage 3).
- Token-exact chunking or semantic (LLM-based) chunking.
- Background or batch ingestion queues; ingestion is synchronous and documents are small.

## Decisions

- **Resource-style API with `PUT /api/knowledge/documents/{id}`.** The client chooses the id
  (`cv`, `project-job-fit-copilot`), so re-sending is naturally idempotent and replaces the document.
  Alternative: `POST` with server ids; needs a separate update path and makes duplicates easy.
- **Embeddings through `IEmbeddingGenerator<string, Embedding<float>>`, provider by configuration.**
  The AppHost adds an `embeddings` model resource next to `chat` for the provider in `Llm:Provider`,
  with the model in `Llm:EmbeddingModel`. The API registers it with the matching Aspire client
  integration (`AddOpenAIClient("embeddings").AddEmbeddingGenerator()` /
  `AddOllamaApiClient("embeddings").AddEmbeddingGenerator()`) and wraps it in a shared
  `UseOpenTelemetry()` + `UseLogging()` pipeline, as stage 1 does for chat. Sensitive data recording
  stays disabled, so chunk text is never in spans.
- **One vector size for both providers: 768 dimensions.** Defaults are OpenAI
  `text-embedding-3-small` requested with `Dimensions = 768` (the text-embedding-3 models support
  shortened vectors) and Ollama `nomic-embed-text` (768 natively). One `vector(768)` column then works
  with either provider. Vectors from different models are not comparable, so every document records
  its `embedding_model`; switching models means re-ingesting (stage 3 filters by model). The
  ingestion checks the returned count and length and fails with 502 on a mismatch.
  Alternative: one column per provider or the full 1536 dimensions; more schema for no gain here.
- **Contextual chunk header for embedding, verbatim text for storage.** The embedding input is
  `"{title} — {section}\n\n{text}"`, so a chunk like "Led a team of 4" carries where it came from;
  the stored `text` stays a verbatim slice of the content, which stage 3 and 5 need for grounding.
- **Deterministic character-based chunker.** Parse Markdown headings (`#`…`######`) into a section
  path joined by ` > `; within a section, pack whole paragraphs up to 1,500 characters; split a longer
  paragraph at word boundaries into pieces of at most 1,500 characters with ~200 characters of
  overlap. Pure function, fully unit-testable. Alternatives: token-based limits with
  `Microsoft.ML.Tokenizers` (exact but tied to one model's tokenizer); `Microsoft.Extensions.DataIngestion`
  (still preview and more than this needs); LLM-based chunking (cost and non-determinism).
- **Direct Npgsql + `Pgvector` instead of a vector-store abstraction.** Two tables:
  `knowledge_documents (id pk, kind, title, content_hash, embedding_model, chunk_count, ingested_at)`
  and `knowledge_chunks (document_id fk on delete cascade, chunk_index, section, text,
  embedding vector(768), pk (document_id, chunk_index))` with an HNSW index using
  `vector_cosine_ops`. Replacing a document (delete chunks, upsert document, insert chunks) runs in
  one transaction, which gives atomic replacement and the "unchanged on failure" guarantee; listing
  documents is a plain query. The `Pgvector` plugin is enabled through
  `AddNpgsqlDataSource("jobfit", configureDataSourceBuilder: b => b.UseVector())`.
  Alternative: `Microsoft.Extensions.VectorData` with the Semantic Kernel PgVector connector; it
  abstracts the store, but has no cross-collection transactions and no natural place for document
  metadata, and the SQL is part of what this project should show. Stage 3 can still expose
  retrieval behind its own interface.
- **Schema created on first use.** A store initializer runs idempotent DDL (`CREATE EXTENSION IF NOT
  EXISTS vector`, `CREATE TABLE IF NOT EXISTS`…) once per process before the first knowledge query,
  not at startup, so the API (and `JobFit.Api.Tests` with its unreachable database) still start
  without Postgres. Alternative: EF Core migrations; heavier than two tables need for now.
- **Embed before touching the database.** Validation (400) → chunking → content hash check
  (SHA-256 of kind, title, content and embedding model; equal hash returns `unchanged` with no model
  call) → one batched embedding call → transaction. Provider failures and mismatches map to 502
  before any write.
- **Feature folder `Knowledge/` in `JobFit.Api`**: contracts, endpoints, `MarkdownChunker`,
  `KnowledgeIngestionService`, `KnowledgeStore`, embedding registration.
- **Tests.**
  - `JobFit.Api.Tests`: chunker unit tests; validation scenarios over HTTP (they fail before the
    database is touched) with a `FakeEmbeddingGenerator` that counts calls; the trace scenario with an
    in-memory `ActivityListener`.
  - `JobFit.IntegrationTests`: a `WebApplicationFactory<Program>` pointed at a `pgvector/pgvector:pg17`
    container started with Testcontainers, with the fake embedding generator, covering create, list,
    chunks, replace, unchanged, delete, 404 and the 502 paths (stored data unchanged).
  - `JobFit.Evals`: an opt-in check (skipped without `OPENAI_API_KEY` / `OLLAMA_ENDPOINT`) that the
    real provider returns 768-dimension vectors and that a CV chunk is closer to a related phrase than
    to an unrelated one.

## Risks / Trade-offs

- [CV text is personal data stored in plain text] → local single-user database, content excluded
  from telemetry; PII handling is stage 7.
- [Character limits approximate tokens] → 1,500 characters is ~400 tokens, well under the 8k limit
  of both embedding models.
- [Chunks embedded with different models are not comparable] → `embedding_model` per document;
  the hash includes the model, so re-sending after a model change re-embeds.
- [Shortened OpenAI vectors lose a little quality] → negligible at this corpus size; the eval checks
  the similarity ordering.
- [Schema created at runtime instead of migrations] → idempotent DDL in one place; migrations can be
  introduced when the schema changes.
- [Integration tests need Docker] → same requirement as the existing AppHost integration tests.
- [Ollama pulls a second model] → `nomic-embed-text` is ~270 MB; the API waits for it like `chat`.

## Migration Plan

New tables only; nothing existing changes. Rollback is dropping the two tables.
