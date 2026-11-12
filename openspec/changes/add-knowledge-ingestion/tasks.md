# Tasks

## 1. Embedding wiring

- [ ] 1.1 Declare the `embeddings` model resource in the AppHost for both providers from `Llm:EmbeddingModel` (defaults `text-embedding-3-small`, `nomic-embed-text`); verify the AppHost starts with each provider and the AppHost integration tests still pass
- [ ] 1.2 Register `IEmbeddingGenerator` in the API with the matching Aspire client integration, 768 dimensions and a shared OpenTelemetry + logging pipeline; verify the API starts and `/health` still returns 200

## 2. Chunking and storage

- [ ] 2.1 Implement `MarkdownChunker` (heading section paths, paragraph packing up to 1,500 characters, word-boundary split with overlap); verify unit tests for section paths, documents without headings, the 4,000-character paragraph (max length, overlap, verbatim text) and sequential indexes pass
- [ ] 2.2 Enable `Pgvector` on the Npgsql data source and implement `KnowledgeStore` with idempotent schema creation on first use, transactional replace, list, get chunks and delete; verify with the integration tests in group 4
- [ ] 2.3 Implement `KnowledgeIngestionService` (validate, chunk, hash check, batched embedding with count/dimension check, store) and map the four endpoints with 400/404/502 problem details; add examples to `JobFit.Api.http`

## 3. API tests (fake `IEmbeddingGenerator`, no database)

- [ ] 3.1 Add `FakeEmbeddingGenerator` (configurable vectors, dimensions, exception, call count, usage) and register it in `JobFitApiFactory` behind the real pipeline
- [ ] 3.2 Empty and whitespace-only content return 400 and the model is not called
- [ ] 3.3 Content over 50,000 characters returns 400 and the model is not called
- [ ] 3.4 Invalid id (`My CV!`) and invalid kind (`letter`) return 400 and the model is not called
- [ ] 3.5 Embedding span records the model name and input token count and does not contain the document text

## 4. Integration tests (Testcontainers pgvector, fake embeddings)

- [ ] 4.1 Add a pgvector Testcontainers fixture and a `WebApplicationFactory` using it with the fake embedding generator
- [ ] 4.2 New document returns 201 `created`, and its chunks are listed with embeddings stored
- [ ] 4.3 Ingested CV and project are both listed with id, kind, title, chunk count, embedding model and ingestion time
- [ ] 4.4 Markdown `# Experience` / `## Acme` produces a chunk with section `Experience > Acme`
- [ ] 4.5 Long paragraph is stored as several overlapping chunks of at most 1,500 characters
- [ ] 4.6 Chunks of an unknown document return 404
- [ ] 4.7 Changed content returns 200 `updated` and only the new chunks remain
- [ ] 4.8 Identical document put twice returns `unchanged` and the model is called once
- [ ] 4.9 Deleting a document returns 204, it is no longer listed and its chunks return 404; deleting an unknown id returns 404
- [ ] 4.10 Provider error on re-ingestion returns 502 and the previous chunks remain
- [ ] 4.11 Wrong embedding dimensions return 502 and nothing is stored

## 5. Eval and docs

- [ ] 5.1 Add an opt-in embedding eval to `JobFit.Evals` (768 dimensions; related phrase closer than unrelated phrase); verify it is skipped without credentials and passes with Ollama
- [ ] 5.2 Update README (endpoints, embedding models, roadmap status) and AGENTS.md if new conventions appear; verify `openspec validate --all --strict` and `dotnet test JobFit.slnx` pass

## Workflow follow-up

- Archive the change after the PR is merged.
