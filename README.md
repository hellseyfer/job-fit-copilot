# Job Fit Copilot

An AI copilot that analyzes how well a CV fits a job posting. A graph-based agent workflow extracts the
job requirements, retrieves evidence from the candidate's experience (RAG), scores the fit, detects gaps
and drafts a cover letter that a human approves before it is used.

Built as a production-minded portfolio project: spec-driven development, observability from day one,
health probes, automated tests and evals in CI.

> Status: **stage 3 — evidence retrieval**. See the [roadmap](#roadmap).

## Stack

| Concern | Choice |
|---|---|
| Runtime / API | .NET 10, ASP.NET Core Minimal APIs |
| Local orchestration & telemetry | [Aspire](https://aspire.dev) + OpenTelemetry |
| Database / vectors | Postgres with pgvector |
| Agent workflow (planned) | Microsoft Agent Framework workflows |
| LLM abstraction | Microsoft.Extensions.AI (`IChatClient`, `IEmbeddingGenerator`): OpenAI by default, Ollama as a free local alternative |
| Vector store | Microsoft.Extensions.VectorData with the Semantic Kernel PgVector connector |
| Spec-driven development | [OpenSpec](https://github.com/Fission-AI/OpenSpec) |
| CI | GitHub Actions |

## Run it locally

Prerequisites: .NET 10 SDK and Docker.

```bash
dotnet run --project src/JobFit.AppHost
```

This starts Postgres (pgvector image), the language model resource and the API, and opens the Aspire
dashboard (URL printed in the console) with resources, logs, traces and metrics. Every model call shows up
as a GenAI span with the model name and token usage.

### Choosing the language model

The code only depends on `IChatClient` and `IEmbeddingGenerator`; the provider is chosen with configuration
(`Llm:Provider`, `Llm:Model`, `Llm:EmbeddingModel`) in the AppHost.

| Provider | Default chat model | Default embedding model | Setup |
|---|---|---|---|
| `openai` (default) | `gpt-4o-mini` | `text-embedding-3-small` (shortened to 768 dimensions) | Set the key once: `dotnet user-secrets set Parameters:openai-api-key <key> --project src/JobFit.AppHost` (or enter it in the dashboard when prompted) |
| `ollama` | `qwen2.5:3b` | `nomic-embed-text` (768 dimensions) | No key, no cost. Aspire runs an Ollama container and pulls the models on first start (~2.3 GB, slower on CPU) |

```bash
# Free local alternative
dotnet run --project src/JobFit.AppHost -- --Llm:Provider ollama

# Another model of the selected provider
dotnet run --project src/JobFit.AppHost -- --Llm:Model gpt-4.1-mini
```

| Endpoint | Purpose |
|---|---|
| `GET /alive` | Liveness: the process is running (200 even if the database is down) |
| `GET /health` | Readiness: API and database are healthy (503 otherwise) |
| `POST /api/requirements/extract` | Job posting → title, seniority and requirements (category, importance, verbatim excerpt) |
| `PUT /api/knowledge/documents/{id}` | Ingest a CV or project (`kind`, `title`, plain text or Markdown `content`): chunk, embed, store in pgvector. 201 `created`, 200 `updated` or `unchanged` |
| `GET /api/knowledge/documents/{id}/chunks` | The stored chunks in order, with their section path (vectors are not returned) |
| `DELETE /api/knowledge/documents/{id}` | Remove a document and its chunks |
| `POST /api/evidence/search` | Requirements (`id`, `description`) → the most similar stored chunks per requirement, with citation and cosine `score`. Optional `kind` and `top` (1–10, default 3) |

Example request in [`src/JobFit.Api/JobFit.Api.http`](src/JobFit.Api/JobFit.Api.http):

```json
{ "jobPosting": "Senior Backend Engineer at Acme. Requirements: 5+ years of C# and .NET ..." }
```

Every requirement carries an `excerpt` copied from the posting; requirements whose excerpt is not found in
the posting (ignoring case and whitespace) are dropped as hallucinations. Empty or oversized (> 20,000
characters) postings return 400 without calling the model; provider errors or invalid model output return 502.

Knowledge documents are split into chunks of at most 1,500 characters that follow Markdown headings (section
paths such as `Experience > Acme`) and paragraphs; long paragraphs are split at word boundaries with overlap.
Each chunk is stored verbatim with a 768-dimension embedding in the `knowledge_chunks` collection (cosine
HNSW index), created on first use. Re-sending identical content returns `unchanged` without calling the
embedding model; changed content replaces the chunks. Invalid input returns 400; embedding errors return 502
and leave the stored version untouched.

Evidence search embeds all requirement descriptions in one call and, for each requirement, returns the `top`
nearest chunks embedded with the configured model (vectors of other models are not comparable), optionally
restricted to one `kind`. Each item cites `documentId`, `kind`, `title`, `section`, `chunkIndex` and the
verbatim `text`, with `score` = 1 − cosine distance. There is no minimum score: deciding whether evidence is
enough is left to fit scoring. Invalid input (1–50 requirements, unique ids, descriptions up to 1,000
characters) returns 400 without calling the model; embedding errors return 502.

## Tests

```bash
dotnet test JobFit.slnx
```

- `tests/JobFit.Api.Tests` — in-memory host with an unreachable database and fake models (`IChatClient`,
  `IEmbeddingGenerator`); fast, no Docker, no API key.
- `tests/JobFit.IntegrationTests` — boots the whole AppHost with a real Postgres container, and runs knowledge
  ingestion and evidence search against a pgvector Testcontainers database with fake embeddings (requires Docker).
- `tests/JobFit.Evals` — runs the extractor against a real model over a golden set of job postings and
  reports must-have recall, checks the embedding model (768 dimensions, related text ranks closer), and
  measures evidence recall@3 on a golden CV (in-memory vector store, no database).
  Skipped unless a model is configured:

```bash
OPENAI_API_KEY=<key> dotnet test tests/JobFit.Evals --logger "console;verbosity=detailed"
OLLAMA_ENDPOINT=http://localhost:11434 dotnet test tests/JobFit.Evals --logger "console;verbosity=detailed"
```

`JOBFIT_EVAL_MODEL` and `JOBFIT_EVAL_EMBEDDING_MODEL` override the models (defaults: `gpt-4o-mini` /
`text-embedding-3-small`, `qwen2.5:3b` / `nomic-embed-text`).

## Repository layout

```
src/
  JobFit.AppHost/          Aspire app model: API + Postgres + language model (OpenAI or Ollama)
  JobFit.ServiceDefaults/  OpenTelemetry, health checks, resilience, service discovery
  JobFit.Api/              HTTP API (feature folders: Requirements/, Knowledge/, Evidence/)
tests/
  JobFit.Api.Tests/
  JobFit.IntegrationTests/
  JobFit.Evals/            Opt-in evals against a real model (golden sets in golden/)
AGENTS.md                  Rules for AI coding agents
openspec/                  Specs (source of truth for behavior) and change proposals
.agents/skills/            OpenSpec workflows for AI coding agents (propose / apply / archive)
```

## Spec-driven workflow

Every feature starts as an OpenSpec change in `openspec/changes/<change>/`:

1. `proposal.md` — why, what changes, what is out of scope.
2. `specs/<capability>/spec.md` — behavior as requirements with WHEN/THEN scenarios.
3. `design.md` — decisions and trade-offs.
4. `tasks.md` — implementation checklist; every scenario gets a test (later, an eval case).

When the change is implemented, `openspec archive <change>` merges its spec deltas into
`openspec/specs/`, which always describes the current behavior. CI runs `openspec validate --all --strict`.

## Roadmap

| Stage | Change | Outcome |
|---|---|---|
| 0 | `setup-solution` | Aspire + API + Postgres, health probes, CI |
| 1 | `add-requirement-extraction` | Structured requirements from a job posting |
| 2 | `add-knowledge-ingestion` | CV and projects embedded into pgvector |
| 3 | `add-evidence-retrieval` | Cited evidence from the CV for each requirement |
| 3b | `add-fit-scoring` | Fit score, strengths and gaps with cited evidence |
| 4 | `add-workflow-graph` | Graph workflow with branching by score |
| 5 | `add-cover-letter-and-validation` | Grounded cover letter with anti-hallucination loop |
| 6 | `add-human-approval` | Pause, checkpoint and resume on human approval |
| 7 | `add-guardrails` | Prompt-injection and PII guardrails |
| 8 | `add-evaluation-suite` | Golden dataset and quality gate in CI |
| 9 | `add-observability-and-cost` | Token/cost/latency per run, Langfuse export |
| 10 | `deploy` | Containerized deployment |
