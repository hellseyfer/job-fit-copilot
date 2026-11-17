# Proposal

## Why

Each analysis run makes multiple chat and embedding model calls (screening, requirement extraction, fit judging,
cover-letter writing/verification or study planning, and revision rounds on approval), and today the API only
emits raw per-call GenAI spans without aggregating tokens, estimated cost or latency per run, or exporting
traces to Langfuse.

## What Changes

- Track token usage (`inputTokens`, `outputTokens`, `totalTokens`), estimated cost (`costUsd`), wall-clock latency
  (`durationMs`) and model call count (`modelCalls`) per analysis run across `IChatClient` and
  `IEmbeddingGenerator` calls in `UseJobFitPipeline()`.
- Expose `usage` on `AnalyzeJobResponse` (persisted with the run in `analysis_runs` and accumulated across approval
  resumes) and emit OpenTelemetry metrics (`jobfit.analysis.tokens`, `jobfit.analysis.cost.usd`,
  `jobfit.analysis.duration.ms`) and span attributes on `JobFit.Observability`.
- Compute cost from a configurable pricing table (`Observability:ModelPricing`) with built-in defaults for OpenAI
  models (`gpt-4o-mini`, `gpt-4.1-mini`, `text-embedding-3-small`) and zero cost for local/unknown models.
- Add opt-in Langfuse trace export via OpenTelemetry OTLP HTTP/protobuf (`Observability:Langfuse:Endpoint`,
  `PublicKey`, `SecretKey`) in `JobFit.ServiceDefaults`, keeping prompt and completion text out of telemetry.

## Capabilities

### New Capabilities
- `observability-and-cost`: per-run token, cost and latency tracking, OpenTelemetry metrics/span enrichment,
  configurable model pricing, and opt-in Langfuse OTLP trace export.

### Modified Capabilities
- `job-analysis-workflow`: `POST /api/analyses`, `GET /api/analyses/{runId}` and `POST /api/analyses/{runId}/approval`
  responses include `usage` with accumulated run tokens, cost, duration and model call count.

## Impact

- Code: `src/JobFit.ServiceDefaults/Extensions.cs` (`JobFit.Observability` meter and Langfuse OTLP exporter),
  `src/JobFit.Api/Observability/` (`UsageTracker`, `UsageTrackingChatClient`, `UsageTrackingEmbeddingGenerator`,
  `ObservabilityOptions`, `CostCalculator`), `src/JobFit.Api/Llm/` (`UseJobFitPipeline`), and
  `src/JobFit.Api/Analysis/` (`AnalyzeJobResponse`, `JobAnalysisWorkflow`, `AnalysisRunStore`).
- Tests: API tests for token/cost/latency aggregation, pricing defaults & overrides, `JobFit.Observability` meter
  emissions, Langfuse exporter registration, and integration tests for persisted/accumulated `usage` across pause
  and resume.
- Docs: `README.md` (usage field, pricing config, Langfuse setup, roadmap status updated to stage 9).

## Out of scope

- Blocking requests on hard token or dollar budgets / rate limiting.
- Sending prompt or completion text to Langfuse or any exporter (privacy requirement from `input-guardrails` remains).
- A dedicated Langfuse REST client or SDK (standard OTLP HTTP/protobuf exporter is used).
