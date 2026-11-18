# Tasks

## 1. Snapshot model pricing fix and Development CORS

- [x] 1.1 Update `CostCalculator` in `src/JobFit.Api/Observability/CostCalculator.cs` to match dated snapshot model IDs (e.g. `gpt-4o-mini-2024-07-18`) by longest prefix and verify with a test in `tests/JobFit.Api.Tests/ObservabilityTests.cs`
- [x] 1.2 Enable Development CORS for `http://localhost:4200` in `src/JobFit.Api/Program.cs`

## 2. Angular Demo UI (`src/JobFit.Web/`)

- [x] 2.1 Scaffold Angular 22 standalone app in `src/JobFit.Web/` with `proxy.conf.json` and typed `JobFitApiService` covering knowledge ingestion, analysis, approval and problem-details error parsing
- [x] 2.2 Implement the demo UI components (knowledge ingestion panel with `.txt`/`.md` file load, job analysis form, fit score/assessments/evidence citations, study plan, cover letter with approval/feedback loop, usage & cost bar, and error banner)
- [x] 2.3 Add unit tests in `src/JobFit.Web/` covering `JobFitApiService` and UI state transitions (`weak-fit`, `strong-fit` approval, `422` prompt-injection error) and verify `npm test` and `npm run build` pass

## 3. Docs and verification

- [x] 3.1 Document running `src/JobFit.Web/` in `README.md`
- [x] 3.2 Run `openspec validate --all --strict` and `dotnet test tests/JobFit.Api.Tests` and verify all checks pass
