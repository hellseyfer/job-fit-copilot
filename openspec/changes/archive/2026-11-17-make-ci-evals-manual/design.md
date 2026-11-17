# Design

## Context

See `proposal.md` - Why.

## Goals / Non-Goals

**Goals:**
- Keep `ci.yml` (`build-and-test`) fast on every push and pull request.
- Preserve the exact `evals` quality-gate job (`JOBFIT_EVAL_REQUIRED=true`, `JOBFIT_EVAL_REPORT=$GITHUB_STEP_SUMMARY`,
  cached Ollama `qwen2.5:3b` and `nomic-embed-text`, artifact upload) in `.github/workflows/evals.yml` triggered on
  demand via `workflow_dispatch`.

**Non-Goals:**
- Automatic path-filtered triggers (explicitly manual only).

## Decisions

### D1. Separate `.github/workflows/evals.yml` with `workflow_dispatch`
Moving `evals` to its own workflow file with `on: workflow_dispatch:` keeps `ci.yml` clean and avoids triggering
Ollama downloads/inference on unrelated commits or PRs, while allowing the user to run the quality gate on any
branch from the GitHub Actions UI.

## Risks / Trade-offs

- [Regressions in prompts or models are not caught automatically on PRs] → Run the `Evals` workflow manually via
  GitHub Actions before merging prompt, model, or guardrail changes.
