# Proposal

## Why

Stage 2 stores the candidate's CV and projects as embedded chunks, but nothing reads them yet. Fit
scoring needs, for every job requirement, the passages of the candidate's documents that are most
likely to support it, with enough source information to cite them. This change adds that retrieval
step (the "R" in RAG) on its own, so its quality can be inspected and evaluated before an LLM starts
judging fit on top of it.

## What Changes

- Add `POST /api/evidence/search`: receives a list of requirements (the `id` and `description`
  produced by `POST /api/requirements/extract`, extra fields ignored), an optional `kind` filter
  (`cv` or `project`) and an optional `top` (1–10, default 3), and returns, per requirement, the most
  similar stored chunks with their document, section, verbatim text and a relevance score.
- Embed all requirements in one batched call with the configured `IEmbeddingGenerator` and search the
  `knowledge_chunks` collection by cosine similarity, only among chunks embedded with the same model.
- Validate input (empty or too many requirements, missing or duplicate ids, empty or oversized
  descriptions, invalid `top` or `kind`) before calling the model; map embedding failures to 502.
- Trace the query embedding call with OpenTelemetry like ingestion does.
- Add a retrieval eval (golden CV + requirements, recall@3 against the real embedding model).

Out of scope: fit scoring, strengths and gaps (`add-fit-scoring`, next change), a minimum relevance
threshold or "no evidence" decision (fit scoring makes it), hybrid/keyword search and re-ranking,
query rewriting with an LLM, caching of query embeddings, the workflow graph, authentication and
multiple users.

## Capabilities

### New Capabilities
- `evidence-retrieval`: finding and citing the stored candidate chunks most relevant to each job
  requirement.

### Modified Capabilities
<!-- none -->

## Impact

- New feature folder `Evidence/` in `JobFit.Api` with the contracts, endpoint and retrieval service;
  it reuses the `knowledge_chunks` collection and the embedding generator registered in stage 2.
- No schema change and no new infrastructure. The eval adds the SK in-memory vector store connector
  (`Microsoft.SemanticKernel.Connectors.InMemory`) so it runs without a database.
- `FakeEmbeddingGenerator` gains configurable vectors per input so tests can control ranking.
- New eval in `JobFit.Evals` (skipped without model credentials).
