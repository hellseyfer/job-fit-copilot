# Design

## Context

See `proposal.md` - Why.

## Goals / Non-Goals

**Goals:**
- Keep `compose.yaml` working identically with zero extra configuration (`API_ORIGIN` defaults to `http://api:8080`).
- Zero fixed monthly infrastructure cost by using `ghcr.io` (free for public repos), Neon free tier (`pgvector`),
  and Azure Container Apps Consumption plan with `min-replicas 0`.

**Non-Goals:**
- Custom domain DNS / TLS setup beyond ACA's built-in `*.azurecontainerapps.io` HTTPS.

## Decisions

### D1. `nginx:alpine` `/etc/nginx/templates/default.conf.template`
`nginx:alpine` runs `envsubst` over `/etc/nginx/templates/*.template` before starting Nginx. Limiting `envsubst`
to `${API_ORIGIN}` (via `NGINX_ENVSUBST_FILTER=API_ORIGIN` or standard `envsubst` since no Nginx `$variables`
collide with `${API_ORIGIN}`) preserves `$host`, `$remote_addr`, `$uri`, etc. Setting `NGINX_ENVSUBST_FILTER=API_ORIGIN`
makes it 100% immune to any `$` variable collision.

### D2. Scale-to-zero (`min-replicas 0`) on Azure Container Apps
Both `jobfit-api` and `jobfit-web` use `--min-replicas 0 --max-replicas 1` on the ACA Consumption plan so idle
time consumes zero vCPU/GiB-seconds, keeping the demo within ACA's monthly free grant.
