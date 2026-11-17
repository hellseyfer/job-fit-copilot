# Proposal

## Why

Stages 1–7 added individual component evals in `JobFit.Evals`, but none of them runs the full analysis pipeline end
to end from a raw job posting to a verified cover letter or study plan, and every eval is skipped when no model is
configured. As a result, CI passes `dotnet test` with zero real-model checks, produces no summary of eval metrics,
and cannot block a prompt or model regression.

## What Changes

- Add an end-to-end golden dataset (`tests/JobFit.Evals/golden/end-to-end.json`, with a golden project alongside the
  golden CV) and `EndToEndEvals` that runs the full analysis pipeline (`InputGuard` → `RequirementExtractor` →
  `FitScoringService` → route branch → `CoverLetterWriter` + `CoverLetterVerifier` or `StudyPlanner`) across labeled
  strong-fit, weak-fit and prompt-injection postings, checking route accuracy, must-have recall, cover-letter
  grounding (no facts absent from the candidate's documents) and study-plan gap coverage.
- Add a shared evaluation harness and report (`EvalHarness`, `EvalReport`):
  - When `JOBFIT_EVAL_REQUIRED=true`, missing model configuration fails the test instead of skipping it, so a CI
    quality gate cannot pass silently.
  - Every eval records its metric, measured score, threshold and pass/fail status; when `JOBFIT_EVAL_REPORT` is set,
    the suite writes a Markdown summary table (compatible with `$GITHUB_STEP_SUMMARY`) and a JSON report.
- Add the CI quality gate in `.github/workflows/ci.yml` that runs `JobFit.Evals` with `JOBFIT_EVAL_REQUIRED=true`
  against Ollama (`qwen2.5:3b` and `nomic-embed-text`, cached across runs, or OpenAI when `OPENAI_API_KEY` is set),
  writes the Markdown report to the job summary, uploads the JSON report as an artifact, and fails the workflow when
  any metric misses its threshold.
- Record the requirement-extraction eval threshold in the `requirement-extraction` specification so every AI
  capability in `openspec/specs/` defines its quality bar.
- Harden `CoverLetterWriter` / `CoverLetterGrounding` (schema field order and citation alignment for multi-chunk or
  misnumbered sentences) and `StudyPlanner` (prompt instructions for partial/nice-to-have gaps and non-contiguous
  IDs) for small local models (`qwen2.5:3b`) surfaced by the CI quality gate.

## Capabilities

### New Capabilities
- `evaluation-suite`: end-to-end workflow evaluation over a golden dataset, required-mode enforcement so quality
  gates cannot skip silently, structured Markdown/JSON metric reporting, and the CI quality gate.

### Modified Capabilities
- `requirement-extraction`: specifies the must-have recall evaluation threshold on the golden job postings.

## Impact

- Code & tests: `tests/JobFit.Evals/` (`EvalHarness`, `EvalReport`, `EndToEndEvals`, `golden/end-to-end.json`, and
  existing eval classes wired into the report and required-mode check); deterministic tests for `EvalHarness` and
  `EvalReport`.
- CI: `.github/workflows/ci.yml` gains an `evals` quality-gate job with cached Ollama models and step summary output.
- Docs: `README.md` (evaluation suite table, `JOBFIT_EVAL_REQUIRED`, `JOBFIT_EVAL_REPORT`, CI quality gate, roadmap
  status updated to stage 8).

## Out of scope

- Changing API endpoints, prompts or runtime business logic in `src/JobFit.Api`.
- LLM-as-judge grading of prose style or tone (only factual grounding, citations, route and recall are gated).
- Paid cloud model calls required on every PR without secrets (the default CI gate uses local Ollama).
- Token, cost and latency budgets per run and Langfuse export (stage 9).
