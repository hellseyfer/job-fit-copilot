# Spec Delta

## MODIFIED Requirements

### Requirement: API and Web UI are packaged as multi-stage container images
The repository SHALL provide a multi-stage `src/JobFit.Api/Dockerfile` running as a non-root user on port 8080
and a multi-stage `src/JobFit.Web/Dockerfile` serving the Angular production bundle via Nginx and proxying `/api/`
and health endpoints to `API_ORIGIN` (defaulting to `http://api:8080`).

#### Scenario: API container configuration
- **WHEN** `src/JobFit.Api/Dockerfile` is inspected or built
- **THEN** it compiles `JobFit.Api` with the .NET 10 SDK image, runs on `mcr.microsoft.com/dotnet/aspnet:10.0` as non-root on port 8080, and does not bake secrets into the image

#### Scenario: Web UI container proxies API traffic
- **WHEN** `src/JobFit.Web/Dockerfile` and `src/JobFit.Web/nginx.conf` are built
- **THEN** Nginx serves the Angular SPA on port 80 and proxies `/api/`, `/health` and `/alive` to `${API_ORIGIN}` (default `http://api:8080`)

## ADDED Requirements

### Requirement: Container images are published to GHCR and deployable to Azure Container Apps
A GitHub Actions workflow SHALL publish the API and Web UI images to `ghcr.io`, and `deploy/azure/deploy.sh`
SHALL deploy both Container Apps (`min-replicas 0`, `max-replicas 1`) wired to an external PostgreSQL (`pgvector`)
connection string and OpenAI key.

#### Scenario: Azure Container Apps deployment assets
- **WHEN** `.github/workflows/publish-images.yml` and `deploy/azure/deploy.sh` are inspected
- **THEN** the workflow pushes both images to `ghcr.io` and the deploy script configures `jobfit-api` and `jobfit-web` with `API_ORIGIN` and secret connection strings
