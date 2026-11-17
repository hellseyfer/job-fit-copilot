# Tasks

## 1. Usage tracking, cost calculation and metrics

- [x] 1.1 Add `ObservabilityOptions`, `CostCalculator`, `RunUsage`, `UsageTracker`, `UsageTrackingChatClient` and `UsageTrackingEmbeddingGenerator` in `src/JobFit.Api/Observability/`; wire into `UseJobFitPipeline()` and verify with unit tests in `tests/JobFit.Api.Tests/ObservabilityTests.cs` covering token aggregation, default/overridden/unknown model pricing and `JobFit.Observability` meter emissions
- [x] 1.2 Register `JobFit.Observability` meter and opt-in Langfuse OTLP trace exporter (`Observability:Langfuse`) in `src/JobFit.ServiceDefaults/Extensions.cs`; verify with tests in `tests/JobFit.Api.Tests/ObservabilityTests.cs` for configured vs unconfigured Langfuse

## 2. Workflow & endpoint integration

- [x] 2.1 Add `Usage` to `AnalyzeJobResponse` and accumulate it across `JobAnalysisWorkflow.StartAsync` and `DecideAsync` (and `AnalysisEndpoints` screening); verify with API and integration tests (`tests/JobFit.IntegrationTests/AnalysisTests.cs`) that `usage` is returned, persisted and accumulated on approval resume

## 3. Documentation and verification

- [x] 3.1 Update `README.md` (status updated to stage 9, `usage` response field, `Observability:ModelPricing` and `Observability:Langfuse` configuration)
- [x] 3.2 Run `openspec validate --all --strict` and `dotnet test JobFit.slnx` and verify all checks pass

## Workflow follow-up

- Archive the change with `openspec archive add-observability-and-cost` after implementation and review.
