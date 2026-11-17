# Design

## Context

See `proposal.md` - Why. Stages 1–7 built seven eval classes in `tests/JobFit.Evals` against golden files in
`tests/JobFit.Evals/golden/`. Each class creates its own `IChatClient` / `IEmbeddingGenerator`, skips with
`Skip.If` when neither `OPENAI_API_KEY` nor `OLLAMA_ENDPOINT` is set, and writes unstructured lines to
`ITestOutputHelper`. In `.github/workflows/ci.yml`, `dotnet test JobFit.slnx` runs with no model configured, so
every eval is skipped.

## Goals / Non-Goals

**Goals:**
- One end-to-end golden dataset and eval that exercises the full analysis pipeline (`InputGuard`, extraction,
  retrieval, fit scoring, branching, and cover-letter verification loop or study plan) against the golden CV and a
  golden project document without needing PostgreSQL.
- A single `EvalHarness` and `EvalReport` used by every eval so missing models fail when `JOBFIT_EVAL_REQUIRED=true`
  and all metrics are written to a Markdown summary table and a JSON file when `JOBFIT_EVAL_REPORT` is set.
- A keyless, zero-cost CI quality gate in `.github/workflows/ci.yml` using Ollama (`qwen2.5:3b` and
  `nomic-embed-text`, cached across workflow runs) or OpenAI if `OPENAI_API_KEY` is provided.

**Non-Goals:**
- Replacing `JobFit.IntegrationTests` (which already test the Agent Framework `RequestPort` and PostgreSQL
  checkpointing with fake models).
- Adding LLM-graded style metrics that would make the CI gate noisy on small models.

## Decisions

### D1. End-to-end eval over an in-memory knowledge store
`golden/end-to-end.json` defines a golden project document (complementing the golden CV in
`evidence-retrieval.json`), a list of terms absent from both documents, and labeled postings:
- `strong-fit` (a senior .NET / PostgreSQL backend role matching the CV and project),
- `weak-fit` (a role dominated by skills absent from the CV and project, such as Rust, Salesforce and German),
- `prompt-injection` (a posting carrying an instruction override / paraphrased injection).

`EndToEndEvals` ingests both documents into `InMemoryCollection<string, KnowledgeChunkRecord>` with the real
embedding model (through `ContactDataMasker` and `InputGuard`, as the endpoint does) and runs each posting through
`InputGuard` → `RequirementExtractor` → `FitScoringService` → `JobAnalysisWorkflow.IsStrongFit` →
`CoverLetterWriter` + `CoverLetterVerifier` rewrite loop (or `StudyPlanner`).
*Alternative:* booting Testcontainers PostgreSQL inside `JobFit.Evals`. Rejected because the vector store is
already abstracted behind `VectorStoreCollection` and keeping `JobFit.Evals` database-free makes local and CI
runs simpler and faster.

### D2. `EvalHarness` with `JOBFIT_EVAL_REQUIRED`
`EvalHarness` centralizes `RequireChatClient()` and `RequireEmbeddingGenerator()` (plus model name discovery).
When neither `OPENAI_API_KEY` nor `OLLAMA_ENDPOINT` is set:
- if `JOBFIT_EVAL_REQUIRED` is `true` (case-insensitive), it fails the test with `Assert.Fail` naming the missing
  environment variables;
- otherwise it calls `Skip.If(true, ...)` so `dotnet test JobFit.slnx` stays fast and credential-free during
  normal local development and in the `build-and-test` CI job.

### D3. `EvalReport` writes Markdown and JSON on every recorded metric
Each eval calls `EvalReport.Record(EvalEntry)` before asserting its threshold so even a failing metric appears in
the report. When `JOBFIT_EVAL_REPORT` is set to `<path>`, `EvalReport` writes:
- `<path>`: a Markdown table (`| Suite | Metric | Model | Value | Threshold | Status |`) that GitHub Actions
  renders directly when `JOBFIT_EVAL_REPORT=$GITHUB_STEP_SUMMARY`;
- `<path>.json`: the serialized list of entries for programmatic inspection or artifact upload.
File writes are synchronized with a lock so parallel or sequential xUnit tests in the same process update the
report deterministically.
*Alternative:* parsing xUnit `.trx` output in a shell script. Rejected because `.trx` only captures free-form
stdout when a test logs it, not structured metrics and thresholds.

### D4. CI quality gate job in `.github/workflows/ci.yml`
`.github/workflows/ci.yml` keeps `build-and-test` fast and adds an `evals` job (`needs: build-and-test`) that:
1. caches `~/.ollama/models` keyed by the model names (`qwen2.5:3b`, `nomic-embed-text`),
2. installs and starts Ollama and pulls the models when the cache misses,
3. runs `dotnet test tests/JobFit.Evals -c Release` with `OLLAMA_ENDPOINT=http://localhost:11434`,
   `JOBFIT_EVAL_REQUIRED=true` and `JOBFIT_EVAL_REPORT=$GITHUB_STEP_SUMMARY` (`[Collection("Evals")]` keeps eval
   classes sequential against a single local CPU model),
4. uploads the JSON report and test results as workflow artifacts (`if: always()`).

## Risks / Trade-offs

- [CPU inference time on GitHub-hosted runners (~5–12 minutes for the full suite)] → Ollama model weights are
  cached with `actions/cache`, eval classes run sequentially in one xUnit collection to avoid CPU thrashing, and
  `timeout-minutes: 30` bounds the job.
- [Small-model variance near thresholds] → Prompts use temperature 0 and the thresholds (0.75 extraction recall,
  0.80 retrieval/judge/planner/verifier/guardrail recall, ≤ 0.10 guardrail false positives, 0 invented facts) are
  already validated against `qwen2.5:3b`.
