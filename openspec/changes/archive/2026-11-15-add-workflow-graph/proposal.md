# Proposal

## Why

Extraction, retrieval and fit scoring are three separate endpoints that a client must chain by hand, and
nothing happens with the result: a weak fit gives no guidance and a strong fit leads nowhere. The project
promises a graph-based agent workflow; this change introduces it with Microsoft Agent Framework Workflows,
so later stages (cover letter, human approval) are new nodes and edges instead of new endpoints.

## What Changes

- New endpoint `POST /api/analyses` that takes a job posting (and optional document `kind`) and runs a
  workflow graph: `extract-requirements` → `score-fit` → switch on the score:
  - score ≥ `Workflow:FitThreshold` (default 70) → `strong-fit` (terminal node; the cover letter is stage 5);
  - otherwise → `plan-study`, which asks the LLM for a study plan for the gaps and partial matches.
- The study plan is grounded: each item must reference a requirement that is a gap or partial match;
  items for other or unknown requirements are dropped.
- The response returns the extracted posting, the fit result, the route taken, the executed node path and
  the study plan (only on the weak-fit route).
- The workflow emits OpenTelemetry spans per run and per executor, with the existing GenAI spans nested.
- New dependency `Microsoft.Agents.AI.Workflows` (1.28.0, GA); `Microsoft.Extensions.AI` is raised to the
  version it requires, and the deprecated SK PgVector connector is replaced by its renamed successor
  `CommunityToolkit.VectorData.PgVector`, which works with the VectorData version the workflows package needs.
- Existing endpoints (`/api/requirements/extract`, `/api/evidence/search`, `/api/fit/score`) are unchanged.

## Capabilities

### New Capabilities
- `job-analysis-workflow`: end-to-end analysis of a job posting against the stored candidate documents as
  a graph workflow, with branching by fit score and a grounded study plan for weak fits.

### Modified Capabilities

None.

## Impact

- Code: new `src/JobFit.Api/Analysis/` feature folder (workflow definition, `StudyPlanner`, endpoint);
  `Program.cs` registration; `JobFit.ServiceDefaults` traces the workflow activity source.
- Tests: API tests (validation, routing, grounding, errors), pgvector integration tests for both routes and
  telemetry, a real-model study-plan eval.
- Docs: README endpoint table and roadmap, `.http` example.

## Out of scope

- Cover letter generation and its validation loop (stage 5).
- Pausing for human approval, checkpoints and resuming runs; runs are synchronous and not persisted (stage 6).
- Streaming progress (SSE) or background runs.
- Prompt-injection and PII guardrails beyond the existing untrusted-data delimiters (stage 7).
- Cost reporting per run (stage 9).
