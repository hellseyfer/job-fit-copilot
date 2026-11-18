# container-deployment Specification

## Purpose
Packages the API and Angular demo UI as multi-stage OCI containers orchestrated with pgvector via Docker Compose,
and verifies container image builds in CI.

## Requirements

### Requirement: API and Web UI are packaged as multi-stage container images
The repository SHALL provide a multi-stage `src/JobFit.Api/Dockerfile` running as a non-root user on port 8080
and a multi-stage `src/JobFit.Web/Dockerfile` serving the Angular production bundle via Nginx and proxying `/api/`
and health endpoints to the API service.

#### Scenario: API container configuration
- **WHEN** `src/JobFit.Api/Dockerfile` is inspected or built
- **THEN** it compiles `JobFit.Api` with the .NET 10 SDK image, runs on `mcr.microsoft.com/dotnet/aspnet:10.0` as non-root on port 8080, and does not bake secrets into the image

#### Scenario: Web UI container proxies API traffic
- **WHEN** `src/JobFit.Web/Dockerfile` and `src/JobFit.Web/nginx.conf` are built
- **THEN** Nginx serves the Angular SPA on port 80 and proxies `/api/`, `/health` and `/alive` to `http://api:8080`

### Requirement: Docker Compose orchestrates the full stack with health gates
A root `compose.yaml` SHALL define `postgres` (`pgvector/pgvector:pg17` with persistent volume and readiness
check), `api` (waiting for healthy `postgres` and exposing `/health`), `web` (waiting for healthy `api`), and an
optional `ollama` profile, with `.env.example` documenting all environment variables without committing secrets.

#### Scenario: Compose stack dependencies and secrets
- **WHEN** `compose.yaml` is parsed
- **THEN** `api` depends on `postgres` (`service_healthy`), `web` depends on `api` (`service_healthy`), and no secret values are hardcoded

### Requirement: CI verifies container image builds
The CI workflow SHALL build both the API and Web UI Docker images on every push to `main` and pull request.

#### Scenario: CI container build job
- **WHEN** `.github/workflows/ci.yml` runs
- **THEN** the `container-build` job builds `src/JobFit.Api/Dockerfile` and `src/JobFit.Web/Dockerfile`
