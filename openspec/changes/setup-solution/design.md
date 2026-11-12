# Design

## Context

Greenfield repository. The project must be runnable by a reviewer with only the .NET SDK and Docker,
and must show production practices (observability, health probes, CI) before any AI feature exists.

## Goals / Non-Goals

**Goals:**
- `dotnet run --project src/JobFit.AppHost` starts API + Postgres (pgvector) + dashboard.
- Telemetry visible in the Aspire dashboard from the first request.
- Health probes that distinguish "process alive" from "ready to serve".
- CI that builds and runs fast tests and Aspire integration tests.

**Non-Goals:**
- Creating the pgvector extension or any schema (comes with knowledge ingestion).
- Production deployment manifests.

## Decisions

- **Aspire 13 for local orchestration.** Models API + Postgres in C#, injects connection strings,
  and ships an OpenTelemetry dashboard. Alternative: docker-compose + manual OTel + Jaeger; more
  portable but more setup and a weaker demo. Telemetry stays standard OTLP, so production can point
  to any backend (Azure Monitor, Grafana, Langfuse).
- **`pgvector/pgvector` image instead of the default Postgres image.** Same Postgres, with the vector
  extension available for the RAG stage. One database for relational data and embeddings.
- **Npgsql via the Aspire client integration.** Registers `NpgsqlDataSource`, its health check,
  tracing and metrics in one call.
- **Health endpoints mapped in all environments.** The template maps them only in Development;
  orchestrators need them in Production. They only return status text, no internal details.
- **Two test layers.**
  - `JobFit.Api.Tests`: in-memory host (`WebApplicationFactory`) with an unreachable database to test
    the failure paths fast and without Docker.
  - `JobFit.IntegrationTests`: boots the full AppHost (real Postgres container) to test the happy path.
- **Central package versions are not introduced yet**; revisit when the project count grows.

## Risks / Trade-offs

- Integration tests need Docker; GitHub-hosted Ubuntu runners provide it.
- Aspire is .NET-centric; mitigated by keeping telemetry on open standards (OTLP).
