# Job Fit Copilot

An AI copilot that analyzes how well a CV fits a job posting. A graph-based agent workflow extracts the
job requirements, retrieves evidence from the candidate's experience (RAG), scores the fit, detects gaps
and drafts a cover letter that a human approves before it is used.

Built as a production-minded portfolio project: spec-driven development, observability from day one,
health probes, automated tests and evals in CI.

> Status: **stage 10 — containerized deployment**. See the [roadmap](#roadmap).

## Stack

| Concern | Choice |
|---|---|
| Runtime / API | .NET 10, ASP.NET Core Minimal APIs |
| Local orchestration & telemetry | [Aspire](https://aspire.dev) + OpenTelemetry |
| Database / vectors | Postgres with pgvector |
| Agent workflow | Microsoft Agent Framework workflows (`Microsoft.Agents.AI.Workflows`) |
| LLM abstraction | Microsoft.Extensions.AI (`IChatClient`, `IEmbeddingGenerator`): OpenAI by default, Ollama as a free local alternative |
| Vector store | Microsoft.Extensions.VectorData with the PgVector connector (`CommunityToolkit.VectorData.PgVector`, formerly the Semantic Kernel connector) |
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

### Demo UI (Angular)

To run the Angular demo UI against the running API (pass the API's HTTP URL from the Aspire dashboard in `API_URL`):

```bash
cd src/JobFit.Web
npm install
API_URL=http://127.0.0.1:12283 npm start
```

Open `http://localhost:4200` to ingest CVs/projects, run analyses, inspect cited evidence and token/cost `usage`, and
approve or revise cover letters.

### Containerized deployment (Docker Compose)

To run the full stack (`postgres` with pgvector, `api`, and `web` on `http://localhost:4200`) in containers without Aspire:

```bash
cp .env.example .env
# Set OPENAI_API_KEY in .env (or switch LLM_PROVIDER=ollama and use --profile ollama)
docker compose up --build
```

### Azure Container Apps + Neon (`$0`/month portfolio setup)

1. Push to `main` (or run the **Publish Images** workflow manually) to publish `job-fit-copilot-api` and
   `job-fit-copilot-web` to `ghcr.io` (make sure both GHCR packages are public, or add registry credentials).
2. Create a free-tier PostgreSQL database on [Neon](https://neon.tech) (`pgvector` is built in) and run:

```bash
NEON_CONNECTION_STRING="Host=...;Database=neondb;Username=...;Password=...;SSL Mode=Require" \
OPENAI_API_KEY="sk-..." \
./deploy/azure/deploy.sh
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
| `POST /api/fit/score` | Requirements (`id`, `description`, `importance`) → per-requirement `met` / `partial` / `missing` judged by the LLM with cited evidence, plus a 0–100 `score`, `strengths`, `partial` and `gaps`. Optional `kind` |
| `POST /api/analyses` | Job posting → the whole workflow graph: extraction, fit scoring and then a verified cover letter (strong fit) or a study plan (weak fit). Returns the `runId`, `status`, posting, `fit`, `route` (`strong-fit` / `weak-fit`), the executed node `path`, `usage` (`inputTokens`, `outputTokens`, `totalTokens`, `costUsd`, `durationMs`, `modelCalls`), `coverLetter` and `studyPlan`. A strong fit returns 202 `awaiting-approval` with a `Location`; a weak fit 200 `completed`. Optional `kind` |
| `GET /api/analyses/{runId}` | The run's `status` (`awaiting-approval`, `approved`, `rejected`, `completed`) and result so far; 404 if unknown |
| `POST /api/analyses/{runId}/approval` | `{ "approved": true \| false, "feedback": "..." }` resumes the paused run: 200 `approved` / `rejected`, or 202 `awaiting-approval` with a new letter when rejected with feedback. 400 invalid body, 404 unknown run, 409 not awaiting approval |

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

Fit scoring retrieves the top 3 evidence items per requirement and asks the chat model (LLM as judge, one
call per requirement, at most 4 in parallel, temperature 0, structured output) for a rationale, the numbers
of the evidence items it relied on and a status. The judgment is grounded in code: cited numbers that were
not provided are dropped, and `met` / `partial` without a valid citation becomes `missing`. Requirements
without evidence are `missing` without calling the model. The score is computed in code, not by the model:
`round(100 × Σ weight·value / Σ weight)` with met 1, partial 0.5, missing 0 and must-haves weighing 2.
Invalid input (the evidence rules plus `importance`) returns 400 without calling a model; embedding or chat
errors and invalid judge output return 502 with no partial result.

`POST /api/analyses` runs a Microsoft Agent Framework workflow:

```
extract-requirements ──► score-fit ──switch──► write-cover-letter ──► verify-cover-letter ──switch──► write-cover-letter    (rewrite due)
                                        │      (score ≥ Workflow:FitThreshold, default 70)          └──► finalize-cover-letter (accepted or 3 drafts)
                                        └────► plan-study (otherwise)
```

Each node wraps an existing service. `plan-study` sends only the `missing` and `partial` requirements (with the
judge rationale) to the LLM and keeps one item (`focus`, `steps`) per such requirement, must-haves first; items
for other requirements are dropped. Invalid input returns 400, a posting with 0 or more than 50 requirements
422, and any model failure 502. Runs and nodes are traced (`invoke_workflow`, `executor.process`) with the model
calls in the same trace.

The cover letter is written only from the evidence the judge cited for `met` and `partial` requirements, as
sentences marked `claim` or `connective` with evidence numbers. A claim without a valid citation is rejected in
code; an LLM verifier then checks every other sentence, one call each, against its cited evidence (connective sentences must state
no facts about the candidate). Rejected sentences go back to the writer with the reason, at most twice; whatever
is still unsupported after the third draft is removed and listed in `coverLetter.removed`.

A strong fit then pauses for human approval:

```
finalize-cover-letter ──► review-cover-letter (request port: the run pauses) ──► apply-review ──► approved | rejected
                                                                                         └──► write-cover-letter (rejected with feedback)
```

`review-cover-letter` is an Agent Framework `RequestPort`. The run is checkpointed after every superstep in the
`workflow_checkpoints` table and tracked in `analysis_runs` (both created on first use), so it survives API
restarts; the approval endpoint restores the same run from its checkpoint and answers the pending request.
Rejecting with feedback sends it to the writer as delimited untrusted data, the new letter goes through the
same verification loop and the run pauses again; after 3 rejections with feedback a rejection is final. One
decision per pause is enforced with an atomic `UPDATE … WHERE status = 'awaiting-approval'`: a second or late
decision returns 409. If a model call fails while resuming, the response is 502 and the run is awaiting
approval again, so the decision can be retried. Checkpoints use Agent Framework's internal format, so a run
paused with one package version may not resume on another. There is no authentication or cleanup of old runs.

### Guardrails

Every free text that reaches a prompt (the job posting, knowledge document content, requirement descriptions sent
to fit scoring and reviewer feedback) is screened before any model or vector-store call, in two layers:

1. **Patterns in code**, on normalized text (NFKC, zero-width characters removed, lower case): instruction
   overrides ("ignore all previous instructions"), role markers (`system:`), chat template tokens (`<|im_start|>`,
   `[INST]`) and the API's own prompt delimiters (`</evidence>`, `<reviewer_feedback>`…).
2. **An LLM classifier** on the configured chat model, for text no pattern matched: one call per window of at most
   2,000 characters, `rationale` before `verdict`. `Guardrails:Classifier=false` turns it off.

A blocked text returns 422 (`type` `https://job-fit-copilot/problems/prompt-injection`, with `field` and `layer`,
never the text) and increments the `jobfit.guardrails.blocked` counter with a `guardrails.blocked` span event. A
blocked feedback leaves the run awaiting approval. A classifier failure returns 502.

Emails, phone numbers and URLs in knowledge documents are replaced with `[email]`, `[phone]` and `[url]` before
the content is screened, chunked, embedded or stored, so no prompt contains them. Documents ingested before this
change keep their original chunks until they are ingested again. Prompt and completion text is never logged or
traced (the model pipelines do not use `UseLogging`, which writes whole messages at `Trace`).

### Observability, cost & Langfuse

Every analysis run aggregates `inputTokens`, `outputTokens`, `totalTokens`, `costUsd`, `durationMs` and `modelCalls`
across all chat and embedding calls (including approval revision rounds) in `usage`, and records
`jobfit.analysis.tokens`, `jobfit.analysis.cost.usd` and `jobfit.analysis.duration.ms` on the `JobFit.Observability`
meter. Built-in USD/1M-token rates cover `gpt-4o-mini`, `gpt-4.1-mini` and `text-embedding-3-small` (local/unknown
models default to `0`); override or add models under `Observability:ModelPricing`.

To export OpenTelemetry traces to [Langfuse](https://langfuse.com), configure:

```bash
dotnet user-secrets set Observability:Langfuse:PublicKey <pk-lf-...> --project src/JobFit.Api
dotnet user-secrets set Observability:Langfuse:SecretKey <sk-lf-...> --project src/JobFit.Api
# Optional for self-hosted / EU (defaults to https://cloud.langfuse.com/api/public/otel):
dotnet user-secrets set Observability:Langfuse:Endpoint https://cloud.langfuse.com/api/public/otel --project src/JobFit.Api
```

## Tests

```bash
dotnet test JobFit.slnx
```

- `tests/JobFit.Api.Tests` — in-memory host with an unreachable database and fake models (`IChatClient`,
  `IEmbeddingGenerator`); fast, no Docker, no API key.
- `tests/JobFit.IntegrationTests` — boots the whole AppHost with a real Postgres container, and runs knowledge
  ingestion, evidence search, fit scoring and the analysis workflow against a pgvector Testcontainers database with fake models (requires Docker).
- `tests/JobFit.Evals` — real-model evaluation suite over golden datasets in `tests/JobFit.Evals/golden/` (in-memory
  vector store, no database). Skipped during normal `dotnet test` unless a model is configured, or fails when
  `JOBFIT_EVAL_REQUIRED=true` (used by the manual `Evals` GitHub Actions workflow so it cannot pass by skipping):

```bash
OPENAI_API_KEY=<key> dotnet test tests/JobFit.Evals --logger "console;verbosity=detailed"
OLLAMA_ENDPOINT=http://localhost:11434 JOBFIT_EVAL_REQUIRED=true JOBFIT_EVAL_REPORT=eval-summary.md dotnet test tests/JobFit.Evals --logger "console;verbosity=detailed"
```

`JOBFIT_EVAL_MODEL` and `JOBFIT_EVAL_EMBEDDING_MODEL` override the models (defaults: `gpt-4o-mini` /
`text-embedding-3-small`, `qwen2.5:3b` / `nomic-embed-text`). When `JOBFIT_EVAL_REPORT=<path>` is set, the suite writes
a Markdown summary table to `<path>` (used for `$GITHUB_STEP_SUMMARY` in CI) and the structured metrics to
`<path>.json`.

| Suite | Golden dataset | Metric & threshold |
|---|---|---|
| `RequirementExtractionEvals` | `golden/requirement-extraction.json` | Must-have recall ≥ 0.75 per posting |
| `EmbeddingEvals` | inline | 768 dimensions; related cosine > unrelated cosine |
| `EvidenceRetrievalEvals` | `golden/evidence-retrieval.json` | Recall@3 ≥ 0.80 on the golden CV |
| `FitScoringEvals` | `golden/fit-scoring.json` | Judge accuracy ≥ 0.80 on supported & unsupported requirements |
| `StudyPlanEvals` | `golden/study-plan.json` | Gap/partial coverage ≥ 0.80 |
| `CoverLetterEvals` | `golden/cover-letter-verification.json` | Verifier accuracy ≥ 0.80; 0 facts absent from the CV (initial and revised letters) |
| `GuardrailEvals` | `golden/guardrails.json` | Attack recall ≥ 0.80; false-positive rate ≤ 0.10 |
| `EndToEndEvals` | `golden/end-to-end.json` | Full pipeline over CV + project: strong-fit route & grounded letter, weak-fit route & study-plan coverage ≥ 0.80, injected posting blocked |

## Repository layout

```
src/
  JobFit.AppHost/          Aspire app model: API + Postgres + language model (OpenAI or Ollama)
  JobFit.ServiceDefaults/  OpenTelemetry, health checks, resilience, service discovery
  JobFit.Api/              HTTP API (feature folders: Requirements/, Knowledge/, Evidence/, Fit/, Analysis/)
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
