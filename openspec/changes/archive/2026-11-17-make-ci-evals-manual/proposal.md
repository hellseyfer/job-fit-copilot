# Proposal

## Why

Running the 14 real-model evals on CPU via Ollama takes ~25–30 minutes on every push and pull request, even when
changes only affect docs, OpenSpec specs, infrastructure, or non-AI code. Moving the evaluation quality gate to a
manual GitHub Actions workflow (`workflow_dispatch`) keeps regular CI fast while preserving on-demand quality-gate
runs whenever model behavior or prompts change.

## What Changes

- Move the `evals` quality-gate job out of `.github/workflows/ci.yml` into a standalone workflow
  `.github/workflows/evals.yml` triggered only by `workflow_dispatch` (manual run from GitHub Actions).
- Keep `.github/workflows/ci.yml` focused on `build-and-test` (OpenSpec validation, build, `JobFit.Api.Tests`,
  `JobFit.IntegrationTests`).
- Update `evaluation-suite` spec, `EvalSuiteTests.cs`, and `README.md` to reflect the manual `Evals` workflow.

## Capabilities

### Modified Capabilities
- `evaluation-suite`: the CI quality-gate job runs in a dedicated manual (`workflow_dispatch`) workflow rather than
  on every push/PR in `ci.yml`.

## Impact

- CI: `.github/workflows/ci.yml` (remove `evals` job), `.github/workflows/evals.yml` (new `workflow_dispatch` workflow).
- Tests & Docs: `tests/JobFit.Api.Tests/EvalSuiteTests.cs` (update workflow path assertion), `README.md`.

## Out of scope

- Changing `JobFit.Evals` behavior, thresholds, golden datasets, or `JOBFIT_EVAL_REQUIRED` / `JOBFIT_EVAL_REPORT`.
