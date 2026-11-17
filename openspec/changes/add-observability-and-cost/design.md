# Design

## Context

See `proposal.md` - Why. `ChatClientRegistration` and `EmbeddingGeneratorRegistration` already share
`UseJobFitPipeline()` with `.UseOpenTelemetry()`, and `JobFit.ServiceDefaults` configures OpenTelemetry metrics and
traces (`Experimental.Microsoft.Extensions.AI`, `Microsoft.Agents.AI.Workflows`, `JobFit.Guardrails`).

## Goals / Non-Goals

**Goals:**
- Capture every chat and embedding call's tokens, latency and cost in one place (`UseJobFitPipeline`) without
  changing individual feature services (`RequirementExtractor`, `FitJudge`, `CoverLetterWriter`, etc.).
- Persist and accumulate `usage` across `POST /api/analyses` and `POST /api/analyses/{runId}/approval`.
- Export traces to Langfuse via standard OpenTelemetry OTLP/HTTP without adding a vendor SDK or logging prompts.

**Non-Goals:**
- Per-user billing or hard spending caps.
- Exporting prompt/completion bodies (blocked by `input-guardrails` privacy requirement).

## Decisions

### D1. `AsyncLocal` usage scope in `UseJobFitPipeline()`
`UsageTracker` opens an `AsyncLocal<UsageScope?>` around `JobAnalysisWorkflow.StartAsync` and `DecideAsync` (also
recording `InputGuard` calls when wrapped by `AnalysisEndpoints` or inside the scope). `UsageTrackingChatClient`
and `UsageTrackingEmbeddingGenerator` sit inside `UseJobFitPipeline()`, wrap `GetResponseAsync` / `GenerateAsync`,
measure elapsed time with `Stopwatch`, read `UsageDetails`, compute `costUsd` via `CostCalculator`, emit
`JobFit.Observability` counters, and add the call to the active `UsageScope`.
*Alternative:* passing a mutable tracker into every service constructor. Rejected because it touches every
feature folder and duplicates timing/pricing code.

### D2. Configurable `Observability:ModelPricing`
`ObservabilityOptions` binds `Observability`:
- `ModelPricing`: `Dictionary<string, ModelPrice>` (`InputPerMillionUsd`, `OutputPerMillionUsd`), seeded with
  defaults (`gpt-4o-mini`: 0.15 / 0.60, `gpt-4.1-mini`: 0.40 / 1.60, `text-embedding-3-small`: 0.02 / 0). Unknown
  or local models (e.g. `qwen2.5:3b`, `nomic-embed-text`) resolve to `0`.
- `Langfuse`: `{ Endpoint, PublicKey, SecretKey }`.

### D3. Langfuse via OpenTelemetry OTLP HTTP/protobuf
Langfuse exposes an OTLP trace ingestion endpoint at `<Endpoint>/v1/traces` (default base
`https://cloud.langfuse.com/api/public/otel`) authenticated via `Authorization: Basic <base64(publicKey:secretKey)>`.
`JobFit.ServiceDefaults` already references `OpenTelemetry.Exporter.OpenTelemetryProtocol`, so registering a named
or additional `AddOtlpExporter` when `Observability:Langfuse:PublicKey` and `SecretKey` are non-empty requires zero
new NuGet packages and exports the existing `GenAI` and `Workflow` spans to Langfuse alongside Aspire.

### D4. `AnalyzeJobResponse.Usage`
`AnalyzeJobResponse` adds `RunUsage? Usage` (`InputTokens`, `OutputTokens`, `TotalTokens`, `CostUsd`,
`DurationMs`, `ModelCalls`), defaulting to `null` (`WhenWritingNull`) for backward compatibility with older stored
rows, and populated on every new `StartAsync` / `DecideAsync` run. On `DecideAsync`, the new `RunUsage` is summed
with `claimed.Response.Usage` before persisting back to `analysis_runs`.

## Risks / Trade-offs

- [Provider omits `UsageDetails` (some local models or error paths)] → Null token counts default to `0`; latency and
  `modelCalls` are still recorded.
- [Static default prices drift over time] → `Observability:ModelPricing` overrides any model rate via configuration.
