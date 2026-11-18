# Proposal

## Why

Stage 10 packaged the API and Angular Web UI as Docker containers for local Compose, where Nginx hardcodes
`proxy_pass http://api:8080`. Deploying to Azure Container Apps with an external free-tier `pgvector` database
(Neon) and GitHub Container Registry (`ghcr.io`) requires parameterizing the Nginx upstream origin, publishing
images to `ghcr.io`, and providing an idempotent `az` deployment script.

## What Changes

- Parameterize `src/JobFit.Web/nginx.conf` via Nginx's built-in `/etc/nginx/templates/default.conf.template`
  mechanism using `API_ORIGIN` (default `http://api:8080` for Compose, or `https://<api-fqdn>` on Azure Container
  Apps).
- Add `.github/workflows/publish-images.yml` to build and push `ghcr.io/<owner>/job-fit-copilot-api:latest` and
  `ghcr.io/<owner>/job-fit-copilot-web:latest` on `main` pushes and `workflow_dispatch`.
- Add `deploy/azure/deploy.sh` to provision the Resource Group, Container Apps Environment, `jobfit-api` (scale-to-zero
  0..1, Neon `ConnectionStrings__jobfit`, OpenAI / optional Langfuse secrets), and `jobfit-web` (external HTTPS).
- Update `tests/JobFit.Api.Tests/DeploymentTests.cs` and `README.md`.

## Capabilities

### Modified Capabilities
- `container-deployment`: Nginx upstream origin is configurable via `API_ORIGIN` (defaulting to `http://api:8080`),
  images are published to `ghcr.io`, and an Azure Container Apps + Neon deployment script is provided.

## Impact

- Modified: `src/JobFit.Web/nginx.conf`, `src/JobFit.Web/Dockerfile`, `tests/JobFit.Api.Tests/DeploymentTests.cs`,
  `README.md`.
- New: `.github/workflows/publish-images.yml`, `deploy/azure/deploy.sh`.

## Out of scope

- Provisioning the external Neon database via CLI (users supply `NEON_CONNECTION_STRING`).
- Paid Azure Container Registry or Azure Database for PostgreSQL resources.
