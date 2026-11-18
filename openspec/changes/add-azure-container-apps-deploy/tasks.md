# Tasks

## 1. Configurable Nginx upstream & GHCR publishing

- [x] 1.1 Update `src/JobFit.Web/nginx.conf` and `src/JobFit.Web/Dockerfile` to template `API_ORIGIN` (default `http://api:8080`) via `/etc/nginx/templates/default.conf.template`
- [x] 1.2 Add `.github/workflows/publish-images.yml` to build and push `jobfit-api` and `jobfit-web` to `ghcr.io`

## 2. Azure Container Apps deployment script, tests & docs

- [x] 2.1 Add `deploy/azure/deploy.sh` for Azure Container Apps + Neon + `ghcr.io`
- [x] 2.2 Update `tests/JobFit.Api.Tests/DeploymentTests.cs` and `README.md`, then run `openspec validate --all --strict` and `dotnet test tests/JobFit.Api.Tests`
