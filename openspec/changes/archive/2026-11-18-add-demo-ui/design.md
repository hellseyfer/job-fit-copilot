# Design

## Context

See `proposal.md` - Why.

## Goals / Non-Goals

**Goals:**
- A clean, single-page Angular 22 standalone application in `src/JobFit.Web/` that exercises the full Job Fit
  Copilot API for live demos.
- Zero impact on `JobFit.IntegrationTests` (which boots `JobFit.AppHost`).
- Accurate USD cost calculation when OpenAI returns dated snapshot model IDs (`gpt-4o-mini-2024-07-18`).

**Non-Goals:**
- Adding `JobFit.Web` into `JobFit.AppHost` (which would force `npm` execution inside `JobFit.IntegrationTests`).
- Styling frameworks or heavy UI libraries; clean custom CSS keeps the bundle small and dependency-free.

## Decisions

### D1. Standalone Angular app with dev proxy + Development CORS
`src/JobFit.Web/` uses `proxy.conf.json` (forwarding `/api` to `http://localhost:5000` or `API_URL`) and also
supports pointing directly at a custom API base URL if needed (enabled by Development CORS on `JobFit.Api` for
`http://localhost:4200`).

### D2. Longest-prefix pricing lookup in `CostCalculator`
`CostCalculator.Calculate` checks exact match first (configured, then defaults), then longest prefix followed by
`-` (so `gpt-4o-mini-2024-07-18` matches `gpt-4o-mini` rather than `gpt-4o` if both exist).

## Risks / Trade-offs

- [Aspire assigns a dynamic HTTP port to `JobFit.Api` on each run] → `proxy.conf.json` reads `API_URL` env var (e.g.
  `API_URL=http://127.0.0.1:12283 npm start`) and the UI header also lets the user inspect/override the API base URL.
