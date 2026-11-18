# Tasks

## 1. Container images and Compose stack

- [x] 1.1 Add `.dockerignore` and `src/JobFit.Api/Dockerfile` (multi-stage SDK 10 → ASP.NET 10 non-root runtime on port 8080 with `/alive` healthcheck)
- [x] 1.2 Add `src/JobFit.Web/nginx.conf` and `src/JobFit.Web/Dockerfile` (multi-stage Node 22 → `nginx:alpine` serving the SPA and proxying `/api/`, `/health`, `/alive` to `http://api:8080`)
- [x] 1.3 Add root `compose.yaml` (`postgres` pgvector, `api`, `web`, optional `ollama` profile, health-gated dependencies) and `.env.example`

## 2. CI, tests and documentation

- [x] 2.1 Add `container-build` job to `.github/workflows/ci.yml` building both Dockerfiles
- [x] 2.2 Add `tests/JobFit.Api.Tests/DeploymentTests.cs` verifying Dockerfiles, `nginx.conf`, `compose.yaml`, and `.github/workflows/ci.yml`
- [x] 2.3 Update `README.md` (stage 10 status, containerized deployment instructions) and run `openspec validate --all --strict` and `dotnet test JobFit.slnx`

## Workflow follow-up

- Archive the change with `openspec archive deploy` after implementation and review.
