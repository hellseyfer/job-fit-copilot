# Design

## Context

See `proposal.md` - Why.

## Goals / Non-Goals

**Goals:**
- Reproducible container builds for `JobFit.Api` and `JobFit.Web` with minimal runtime images.
- Single-command local or server startup via `docker compose up --build` (OpenAI via `.env`, or local Ollama via
  `--profile ollama`).
- Fast CI verification of both Dockerfiles without pushing to a registry.

**Non-Goals:**
- Kubernetes/Helm/Terraform cloud manifests (left to the target hosting platform).
- Reverse-proxy TLS certificates (assumed handled by an external ingress/load balancer in production).

## Decisions

### D1. Non-root ASP.NET 10 runtime with `/health` / `/alive`
`src/JobFit.Api/Dockerfile` builds from the repository root so it can restore both `JobFit.Api` and
`JobFit.ServiceDefaults`, publishes Release binaries, and copies them to `mcr.microsoft.com/dotnet/aspnet:10.0`
running as the built-in `app` user (`$APP_UID`) on `ASPNETCORE_HTTP_PORTS=8080`. Note `JobFit.Api` already exposes
`/alive` and `/health` in `Production` (`service-health` spec).

### D2. Nginx reverse proxy in `JobFit.Web`
`src/JobFit.Web/Dockerfile` builds the Angular production bundle with `node:22-alpine` (`npm ci && npm run build`)
and serves `dist/job-fit-web/browser` from `nginx:alpine`. `nginx.conf` proxies `/api/`, `/health` and `/alive` to
`http://api:8080` so the browser talks to a single origin (`http://localhost:4200`) with zero CORS configuration
needed in production.

### D3. Compose environment and optional `ollama` profile
`compose.yaml` uses `ConnectionStrings__jobfit`, `ConnectionStrings__chat`, `ConnectionStrings__embeddings` and
`Llm__Provider` (defaulting to `openai` with `OPENAI_API_KEY` from `.env`, or overridable to `ollama` with the
`ollama` profile). Persistent Docker volumes store `pgvector` data and `ollama` models.

## Risks / Trade-offs

- [Cold `aspnet:10.0` image has no `curl`] → Compose `api` healthcheck uses `wget`/`dotnet` or `/alive` probe, or
  installs `curl`? Better: `mcr.microsoft.com/dotnet/aspnet:10.0` does not include `curl` or `wget` by default; using
  a small `curl` install or `bash`? Even cleaner: use a tiny `HEALTHCHECK` or `compose.yaml` healthcheck using
  `/dev/tcp` in `bash` (or `curl` installed in runtime stage) — installing `curl` via `apt-get` in the runtime stage
  adds ~5 MB and makes `HEALTHCHECK` and Compose `service_healthy` reliable.
