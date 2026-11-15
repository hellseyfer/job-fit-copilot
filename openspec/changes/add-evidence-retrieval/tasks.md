# Tasks

## 1. Retrieval

- [ ] 1.1 Extract the first-use collection creation from `KnowledgeIngestionService` into a shared `KnowledgeCollection` helper; verify the existing API and integration tests still pass
- [ ] 1.2 Add `Evidence/` contracts and `EvidenceRetriever` (one batched embedding call, count/dimension check, `SearchAsync` per requirement filtered by embedding model and optional kind, score = 1 − cosine distance, request order kept); verify with the integration tests in group 3
- [ ] 1.3 Map `POST /api/evidence/search` with validation (requirements 1–50, unique non-empty ids, descriptions 1–1,000 characters, `top` 1–10 default 3, `kind` `cv`/`project`) and 400/502 problem details; add an example to `JobFit.Api.http` and verify it against the AppHost with Ollama

## 2. API tests (fake `IEmbeddingGenerator`, no database)

- [ ] 2.1 Add an optional `VectorFor` function to `FakeEmbeddingGenerator`; verify the existing tests still pass
- [ ] 2.2 Empty and 51-item `requirements` return 400 and the model is not called
- [ ] 2.3 Missing and duplicate ids return 400 and the model is not called
- [ ] 2.4 Empty and 1,001-character descriptions return 400 and the model is not called
- [ ] 2.5 `top` 0, `top` 11 and `kind` `letter` return 400 and the model is not called

## 3. Integration tests (Testcontainers pgvector, fake embeddings)

- [ ] 3.1 Relevant chunk ranks first for its requirement, and results keep the request order
- [ ] 3.2 Evidence is ordered by descending score, identical vectors score 1, and `text`/`section` match the chunks endpoint for the same `chunkIndex`
- [ ] 3.3 Default `top` returns 3 items and `top` 1 returns 1 item per requirement
- [ ] 3.4 `kind` `project` returns only project chunks
- [ ] 3.5 Chunks ingested with another embedding model are ignored, and an empty store returns empty evidence with 200
- [ ] 3.6 Provider error and wrong dimensions return 502
- [ ] 3.7 A search for 3 requirements calls the model once and records an embedding span with the model name and input token count

## 4. Eval and docs

- [ ] 4.1 Add the retrieval eval (golden CV, labeled requirements, recall@3 ≥ 0.8, skipped without credentials); verify it runs and passes with Ollama `nomic-embed-text`
- [ ] 4.2 Update README (endpoint table, how retrieval works, roadmap status) and AGENTS.md if conventions change; verify `openspec validate --all --strict` and `dotnet test JobFit.slnx` pass

## Workflow follow-up

- Open the PR after `add-knowledge-ingestion` (PR #4) is merged, or stacked on it.
- Archive the change after the PR is merged.
