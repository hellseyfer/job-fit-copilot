# Proposal

## Why

Testing Job Fit Copilot currently requires crafting `curl` or `.http` requests by hand, which makes live demos of
the workflow (CV ingestion, requirement extraction, fit scoring, study plan / cover-letter grounding, human approval
loop, guardrails, and per-run token/cost usage) cumbersome. Also, real OpenAI responses return dated snapshot
model IDs (such as `gpt-4o-mini-2024-07-18`) that missed the exact-match pricing lookup in `CostCalculator`.

## What Changes

- Add a lightweight Angular single-page demo UI in `src/JobFit.Web/` covering the end-to-end copilot flow:
  1. **Knowledge Ingestion**: paste or load a `.txt`/`.md` CV or project document (`PUT /api/knowledge/documents/{id}`)
     and view stored chunks (`GET /api/knowledge/documents/{id}/chunks`).
  2. **Job Analysis**: paste a job posting, choose an optional document `kind` (`all`, `cv`, `project`), and run
     `POST /api/analyses`.
  3. **Fit & Workflow Results**: display fit score (0–100), route (`strong-fit` / `weak-fit`), executed node `path`,
     per-run `usage` (`inputTokens`, `outputTokens`, `totalTokens`, `costUsd`, `durationMs`, `modelCalls`), and
     per-requirement `met` / `partial` / `missing` assessments with rationale and cited evidence chunks.
  4. **Study Plan & Cover Letter Approval**: render `studyPlan` for weak fits, or `coverLetter` (sentences with cited
     evidence and any `removed` sentences) with **Approve**, **Reject**, and **Reject with feedback** actions
     (`POST /api/analyses/{runId}/approval`) for strong fits awaiting approval.
  5. **Error & Guardrail Feedback**: surface 400 validation errors, 422 prompt-injection blocks (`field` and `layer`),
     and 502 provider errors clearly in the UI.
- Enable Development CORS for `http://localhost:4200` on `JobFit.Api` and provide an Angular dev proxy (`proxy.conf.json`)
  plus unit tests (`ng test`) for the API service and components.
- Update `CostCalculator` to match dated/versioned model snapshot IDs (e.g. `gpt-4o-mini-2024-07-18` → `gpt-4o-mini`)
  via longest-prefix matching.

## Capabilities

### New Capabilities
- `demo-ui`: Angular web UI for demonstrating CV/project ingestion, job analysis, fit breakdown, study plan,
  cover-letter approval with feedback, usage/cost display, and guardrail/error handling.

### Modified Capabilities
- `observability-and-cost`: model pricing lookup matches dated snapshot IDs (e.g. `gpt-4o-mini-2024-07-18`) to their
  configured or default base model rate.

## Impact

- Code: new `src/JobFit.Web/` Angular application; `src/JobFit.Api/Program.cs` (Development CORS for `localhost:4200`);
  `src/JobFit.Api/Observability/CostCalculator.cs` (prefix pricing match).
- Tests: Angular Vitest/unit tests in `src/JobFit.Web/`; `CostCalculator` unit test in
  `tests/JobFit.Api.Tests/ObservabilityTests.cs`.
- Docs: `README.md` (how to run the Angular demo UI).

## Out of scope

- Authentication, multi-user accounts, or persistent browser history.
- Server-side PDF/DOCX binary parsing (users paste or load plain text / Markdown files).
- Containerizing `JobFit.Web` (handled in stage 10 `deploy`).
