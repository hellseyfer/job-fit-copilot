# Proposal

## Why

Every later stage (RAG, agent workflow, evals) needs the same foundation: one command to run the
API with its database locally, telemetry from day one, health endpoints an orchestrator can probe,
and a CI pipeline that keeps the main branch green. Building it first keeps feature changes small.

## What Changes

- Create the .NET 10 solution: Aspire AppHost, shared ServiceDefaults, the `JobFit.Api` web API and
  test projects.
- Run Postgres with the pgvector extension available as an Aspire-managed resource, with a
  `jobfit` database the API connects to.
- Expose liveness and readiness endpoints in all environments; readiness includes the database.
- Wire OpenTelemetry (traces, metrics, logs) through ServiceDefaults to the Aspire dashboard.
- Add a GitHub Actions workflow that restores, builds and runs all tests on every push and PR.

Out of scope: any AI/LLM functionality, authentication, cloud deployment, Langfuse export.

## Capabilities

### New Capabilities
- `service-health`: liveness and readiness reporting of the API, including its database dependency.

### Modified Capabilities
<!-- none -->

## Impact

- New solution `JobFit.slnx` with projects under `src/` and `tests/`.
- Requires Docker locally and in CI (Postgres container for integration tests).
- Dependencies: Aspire 13 hosting/client integrations (PostgreSQL, Npgsql), xUnit.
