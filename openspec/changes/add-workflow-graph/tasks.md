## 1. Workflow

- [x] 1.1 Add `Microsoft.Agents.AI.Workflows` 1.28.0, raise `Microsoft.Extensions.AI` to 10.11.0, move to `CommunityToolkit.VectorData.PgVector`/`.InMemory`, and trace the `Microsoft.Agents.AI.Workflows` source in ServiceDefaults
- [x] 1.2 Add `Analysis/` contracts (`AnalyzeJobRequest`, `AnalysisResult`, `StudyPlan`, stage records) and `WorkflowOptions` (`FitThreshold`, default 70)
- [x] 1.3 Add `StudyPlanner` with structured output, delimiters, grounding and must-have-first ordering
- [x] 1.4 Add `JobAnalysisWorkflow`: executors, switch edge, outputs, OpenTelemetry, run and error mapping
- [x] 1.5 Add `POST /api/analyses` with validation (400), 422 for 0 or >50 requirements and 502 for model errors

## 2. API tests

- [x] 2.1 Invalid input (empty, too long, bad kind) returns 400 without model calls
- [x] 2.2 Extraction failure returns 502 without embedding calls
- [x] 2.3 No requirements and more than 50 requirements return 422 without embedding calls
- [x] 2.4 Study plan grounding: other/unknown ids dropped, duplicates removed, must-have first
- [x] 2.5 Routing rule (`IsStrongFit`): score at, above and below the threshold
- [x] 2.6 Routing through the graph with a configured threshold (50 and 60 for a score of 60) — in integration tests

## 3. Integration tests (pgvector)

- [x] 3.1 Strong fit: route, path, no study plan, study-plan model not called
- [x] 3.2 Weak fit: route, path and grounded study plan
- [x] 3.3 `kind` `project` restricts cited evidence
- [x] 3.4 Weak fit with no gaps or partials returns an empty plan without a study-plan call
- [x] 3.5 Judge failure and study-plan failure (error and invalid output) return 502
- [x] 3.6 Telemetry: workflow run span, executor spans per node, chat spans in the same trace

## 4. Eval and docs

- [x] 4.1 Study-plan eval with a weak-fit golden posting against the golden CV (coverage ≥ 0.8)
- [x] 4.2 README (endpoint, graph, threshold, roadmap status) and `.http` example
- [x] 4.3 `openspec validate --all --strict` and `dotnet test JobFit.slnx`
