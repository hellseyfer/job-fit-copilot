# Job Fit Copilot

An AI copilot that analyzes how well a CV fits a job posting. A graph-based agent workflow extracts the
job requirements, retrieves evidence from the candidate's experience (RAG), scores the fit, detects gaps
and drafts a cover letter that a human approves before it is used.

Built as a production-minded portfolio project: spec-driven development, observability from day one,
health probes, automated tests and evals in CI.

> Status: **stage 0 — foundation**. No AI features yet. See the [roadmap](#roadmap).

## Stack

| Concern | Choice |
|---|---|
| Runtime / API | .NET 10, ASP.NET Core Minimal APIs |
| Local orchestration & telemetry | [Aspire](https://aspire.dev) + OpenTelemetry |
| Database / vectors | Postgres with pgvector |
| Agent workflow (planned) | Microsoft Agent Framework workflows |
| LLM abstraction (planned) | Microsoft.Extensions.AI (`IChatClient`) |
| Spec-driven development | [OpenSpec](https://github.com/Fission-AI/OpenSpec) |
| CI | GitHub Actions |

## Run it locally

Prerequisites: .NET 10 SDK and Docker.

```bash
dotnet run --project src/JobFit.AppHost
```

This starts Postgres (pgvector image) and the API, and opens the Aspire dashboard (URL printed in the
console) with resources, logs, traces and metrics.

| Endpoint | Purpose |
|---|---|
| `GET /alive` | Liveness: the process is running (200 even if the database is down) |
| `GET /health` | Readiness: API and database are healthy (503 otherwise) |

## Tests

```bash
dotnet test JobFit.slnx
```

- `tests/JobFit.Api.Tests` — in-memory host with an unreachable database; fast, no Docker.
- `tests/JobFit.IntegrationTests` — boots the whole AppHost with a real Postgres container (requires Docker).

## Repository layout

```
src/
  JobFit.AppHost/          Aspire app model: API + Postgres
  JobFit.ServiceDefaults/  OpenTelemetry, health checks, resilience, service discovery
  JobFit.Api/              HTTP API
tests/
  JobFit.Api.Tests/
  JobFit.IntegrationTests/
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
| 3 | `add-evidence-retrieval`, `add-fit-scoring` | Fit score, strengths and gaps with cited evidence |
| 4 | `add-workflow-graph` | Graph workflow with branching by score |
| 5 | `add-cover-letter-and-validation` | Grounded cover letter with anti-hallucination loop |
| 6 | `add-human-approval` | Pause, checkpoint and resume on human approval |
| 7 | `add-guardrails` | Prompt-injection and PII guardrails |
| 8 | `add-evaluation-suite` | Golden dataset and quality gate in CI |
| 9 | `add-observability-and-cost` | Token/cost/latency per run, Langfuse export |
| 10 | `deploy` | Containerized deployment |
