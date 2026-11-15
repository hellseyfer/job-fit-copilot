# Design

## Context

Stage 2 stores chunks in the `knowledge_chunks` VectorData collection (SK PgVector connector):
768-dimension vectors with cosine distance and an HNSW index, plus `DocumentId`, `Kind` (indexed),
`Title`, `Section`, `Text`, `ChunkIndex` and `EmbeddingModel` on every chunk. The API already
registers `IEmbeddingGenerator` (OpenAI `text-embedding-3-small` at 768 dimensions or Ollama
`nomic-embed-text`) behind the shared OpenTelemetry + logging pipeline, and `KnowledgeIngestionService`
creates the collection on first use. Stage 1 returns requirements with `id`, `description`,
`category`, `importance` and `excerpt`. See proposal.md for motivation.

## Goals / Non-Goals

**Goals:**
- One request retrieves evidence for all requirements of a posting, with a single embedding call.
- Every evidence item is citable: the same `documentId`, `section`, `chunkIndex` and verbatim `text`
  that the knowledge API returns.
- Retrieval quality is measurable (recall@3 eval) before fit scoring builds on it.

**Non-Goals:**
- Deciding whether evidence is good enough; scores are returned as-is and fit scoring judges them.
- Tuning the HNSW index, hybrid search or re-ranking.

## Decisions

- **`POST /api/evidence/search` with a batch of requirements.** The body takes the `requirements`
  array that `POST /api/requirements/extract` returns (`id` and `description` are used, other fields
  are ignored), so the two calls can be chained by hand now and by the workflow graph in stage 4.
  `POST` because the input is a body, not a resource. Alternative: `GET ?q=` for one query; simpler
  but needs N round trips and N embedding calls per posting.
- **Query text is the requirement `description`.** It is the normalized phrasing ("5+ years of C#");
  the `excerpt` adds posting noise ("Requirements:"). No LLM query rewriting (cost, latency,
  non-determinism); the eval tells whether it is needed.
- **One batched embedding call, then one vector search per requirement.**
  `embeddingGenerator.GenerateAsync(descriptions)` → check count and 768 dimensions (502 otherwise,
  reusing `EmbeddingFailedException`) → for each vector
  `collection.SearchAsync(vector, top, new VectorSearchOptions<KnowledgeChunkRecord> { Filter = … })`.
  Searches run sequentially; at most 50 small queries against one local table.
- **Filter by `EmbeddingModel` (and optionally `Kind`) in the search.** `Filter = r =>
  r.EmbeddingModel == model && r.Kind == kind`, translated by the connector to a SQL `WHERE`. Vectors
  of different models are not comparable even when they have the same length.
- **Score is cosine similarity.** pgvector returns cosine distance (0 = identical); the API reports
  `1 − distance` so higher means more relevant, which is easier to read and explain. An integration
  test pins the conversion (identical vectors score 1).
- **Collection readiness is shared with ingestion.** The "ensure collection exists on first use" logic
  moves from `KnowledgeIngestionService` to a small shared `KnowledgeCollection` helper used by both
  services, so a search before any ingestion returns empty evidence instead of failing on a missing
  table.
- **Feature folder `Evidence/`**: `Contracts.cs`, `EvidenceEndpoints.cs` (validation, problem
  details), `EvidenceRetriever.cs` (embed + search + map).
- **Tests.**
  - `FakeEmbeddingGenerator` gets an optional `VectorFor(string input)` function, so tests decide
    which chunk is closest to which requirement (e.g. one axis per topic) instead of relying on the
    hash-based vectors.
  - `JobFit.Api.Tests`: every validation scenario returns 400 with `CallCount == 0` (no database).
  - `JobFit.IntegrationTests` (Testcontainers pgvector, fake embeddings): ranking, request order,
    citation fields against the chunks endpoint, default and explicit `top`, `kind` filter, other-model
    chunks ignored, empty store, provider error and wrong dimensions (502), one embedding call with a
    traced span.
  - `JobFit.Evals`: a golden CV (Markdown with sections) and ~10 requirements labeled with the section
    that supports them, plus 2 unsupported ones that are reported but not counted in recall. Ingests
    and searches with the real embedding model through the same chunker and services (in-memory
    collection, no database); skipped without `OPENAI_API_KEY` / `OLLAMA_ENDPOINT`. Threshold:
    recall@3 ≥ 0.8.

## Risks / Trade-offs

- [HNSW with a `WHERE` filter is approximate post-filtering: pgvector may return fewer than `top`
  rows when most neighbours are filtered out] → Data is a handful of documents from one user, far
  below `hnsw.ef_search` (40); the "explicit top" and "kind filter" tests cover it. If it matters
  later, enable pgvector iterative scans or filter by kind with separate collections.
- [Top-k always returns something, even for a requirement the candidate does not meet] → By design:
  the score is returned and fit scoring (next change) decides between strength and gap; a fixed
  threshold would be model-specific.
- [Short requirement descriptions ("Docker") embed poorly against long chunks] → Contextual chunk
  headers from stage 2 help; the eval measures it, and query rewriting stays an option for later.
- [The eval uses an in-memory collection instead of pgvector] → Ranking is the same exact cosine
  similarity; pgvector-specific behavior is covered by the integration tests.

## Migration Plan

No schema change: the stage 2 collection already has every field this change filters on.
