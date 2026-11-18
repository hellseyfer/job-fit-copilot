# Proposal

## Why

Until now Job Fit Copilot can only be run through .NET Aspire (`dotnet run --project src/JobFit.AppHost`) plus a
manual `ng serve` dev server. Stage 10 of the roadmap calls for containerized deployment so the API, the Angular
demo UI and PostgreSQL with pgvector can be built as OCI images and run anywhere via Docker Compose, with CI
verifying that both container images build cleanly.

## What Changes

- Add a multi-stage `src/JobFit.Api/Dockerfile` (SDK 10 build/publish → non-root ASP.NET 10 runtime on port 8080,
  `Production` environment, with a `/alive` health check).
- Add a multi-stage `src/JobFit.Web/Dockerfile` and `src/JobFit.Web/nginx.conf` (Node 22 `ng build` → `nginx:alpine`
  serving the Angular SPA and reverse-proxying `/api/`, `/health` and `/alive` to `api:8080`).
- Add a root `.dockerignore`, `compose.yaml` (orchestrating `postgres` with `pgvector/pgvector:pg17`, `api`, `web`,
  and an optional `ollama` profile), and `.env.example` documenting required and optional environment variables
  (OpenAI / Ollama, Postgres password, optional Langfuse keys).
- Add a `container-build` job in `.github/workflows/ci.yml` that builds both Docker images on every push and PR.
- Add automated verification tests in `tests/JobFit.Api.Tests/DeploymentTests.cs` checking the compose and
  container definitions.

## Capabilities

### New Capabilities
- `container-deployment`: multi-stage Dockerfiles for the API and Angular web UI, Docker Compose stack with
  pgvector and health-gated startup, environment configuration template, and CI container-build verification.

## Impact

- New files: `.dockerignore`, `compose.yaml`, `.env.example`, `src/JobFit.Api/Dockerfile`,
  `src/JobFit.Web/Dockerfile`, `src/JobFit.Web/nginx.conf`, `tests/JobFit.Api.Tests/DeploymentTests.cs`.
- Modified: `.github/workflows/ci.yml` (`container-build` job), `README.md` (container deployment section and
  stage 10 status).

## Out of scope

- Cloud-provider-specific IaC (Terraform, Kubernetes manifests, ECS/ACA Bicep) or TLS termination.
- Publishing images to a remote container registry.
