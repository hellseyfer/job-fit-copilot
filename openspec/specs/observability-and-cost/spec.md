# observability-and-cost Specification

## Purpose
Tracks token usage, estimated cost and latency per analysis run, emits OpenTelemetry metrics, and optionally
exports OpenTelemetry traces to Langfuse over OTLP without recording prompt or completion text.

## Requirements

### Requirement: Model usage and cost are tracked per analysis run
The API SHALL wrap chat and embedding model calls to record input tokens, output tokens, total tokens, estimated
USD cost, duration in milliseconds and model call count for each analysis run, accumulating across approval
resumes.

#### Scenario: Usage is aggregated across multiple model calls
- **WHEN** an analysis executes chat and embedding calls that report token usage
- **THEN** `usage` on the response reports the summed `inputTokens`, `outputTokens`, `totalTokens`, `costUsd`, non-negative `durationMs` and `modelCalls`

#### Scenario: Usage accumulates across approval resume
- **WHEN** a paused strong-fit run is rejected with feedback and resumed
- **THEN** the resumed response's `usage` includes both the initial run's usage and the resume's usage

### Requirement: Cost is calculated from configurable model pricing
The system SHALL compute `costUsd` from `Observability:ModelPricing` (per-million input and output USD rates),
matching exact model IDs or dated snapshot IDs by longest prefix, falling back to built-in defaults for known
OpenAI models and `0` for unknown or local models.

#### Scenario: Known model uses configured or default pricing
- **WHEN** a model call reports input and output tokens for `gpt-4o-mini`, a dated snapshot such as `gpt-4o-mini-2024-07-18`, or an overridden entry in `Observability:ModelPricing`
- **THEN** `costUsd` equals `(inputTokens * inputRate + outputTokens * outputRate) / 1_000_000`

#### Scenario: Unknown or local model has zero cost
- **WHEN** a model call uses a model with no configured pricing (such as `qwen2.5:3b`)
- **THEN** `costUsd` is `0`

### Requirement: Observability metrics are emitted
Each completed or paused analysis SHALL emit `jobfit.analysis.tokens` (tagged with `model` and `kind`),
`jobfit.analysis.cost.usd` (tagged with `model`) and `jobfit.analysis.duration.ms` (tagged with `route` and `status`)
on the `JobFit.Observability` meter.

#### Scenario: Metrics are recorded for an analysis run
- **WHEN** an analysis run finishes
- **THEN** `jobfit.analysis.tokens`, `jobfit.analysis.cost.usd` and `jobfit.analysis.duration.ms` record measurements on `JobFit.Observability`

### Requirement: Opt-in Langfuse trace export
When `Observability:Langfuse:PublicKey` and `Observability:Langfuse:SecretKey` are configured, ServiceDefaults SHALL
register an OTLP HTTP/protobuf trace exporter targeting `Observability:Langfuse:Endpoint` (defaulting to
`https://cloud.langfuse.com/api/public/otel`) with Basic authentication. When credentials are absent, no Langfuse
exporter SHALL be registered.

#### Scenario: Langfuse exporter is registered when configured
- **WHEN** `Observability:Langfuse:PublicKey` and `SecretKey` are set
- **THEN** the OpenTelemetry tracer provider includes an OTLP exporter targeting the configured Langfuse endpoint

#### Scenario: Langfuse exporter is skipped when unconfigured
- **WHEN** `Observability:Langfuse:PublicKey` or `SecretKey` is missing
- **THEN** the application starts normally without registering a Langfuse exporter
