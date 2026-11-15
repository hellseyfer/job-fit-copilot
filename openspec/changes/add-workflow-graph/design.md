# Design

## Context

Stages 1–3b expose extraction (`RequirementExtractor`), retrieval (`EvidenceRetriever`) and fit scoring
(`FitScoringService`) as independent services and endpoints. Stage 4 composes them into a graph using
Microsoft Agent Framework Workflows (`Microsoft.Agents.AI.Workflows`, GA since 1.x; this change uses
1.28.0, which requires `Microsoft.Extensions.AI` 10.11.0, so the API project moves from 10.10.0).

## Goals / Non-goals

Goals: a real graph (executors, edges, a conditional switch) that later stages extend with nodes; reuse
of the existing services without duplicating logic; deterministic, testable routing; traces per node.

Non-goals: checkpoints, human-in-the-loop requests and persistence (stage 6), streaming, cover letter
(stage 5).

## Decisions

- **Graph.**
  ```
  extract-requirements ──► score-fit ──switch──► strong-fit   (score ≥ threshold)
                                          └────► plan-study   (default)
  ```
  Built with `WorkflowBuilder(extract)`, `AddEdge(extract, score)`, `AddSwitch(score, s => s.AddCase<FitStage>(f => f.Fit.Score >= threshold, [strong]).WithDefault([plan]))`
  and `WithOutputFrom(strong, plan)`. The terminal nodes yield the `AnalysisResult`.
- **Executors are thin function bindings** (`BindAsExecutor`) over the existing services, so the graph
  only expresses orchestration. Messages between nodes are immutable records that accumulate state
  (`ExtractedStage(posting, kind)`, `FitStage(posting, fit)`), so each node is a pure function of its input
  and is unit-testable without the graph. The `path` in the response comes from the executor-completed
  events, so it reflects what actually ran.
- **A new workflow per request.** The workflow is built per request (cheap: a few bindings) because the
  threshold and `kind` are request-scoped and in-process runs are short. Execution uses
  `InProcessExecution.RunAsync`; outputs come from `WorkflowOutputEvent`, failures from
  `ExecutorFailedEvent` / `WorkflowErrorEvent`, mapped back to the original exception type
  (`InvalidModelOutputException`, chat errors, `EmbeddingFailedException`, `JudgeFailedException`,
  `StudyPlanFailedException`) → 502, `NoScorableRequirementsException` → 422.
- **Threshold** `Workflow:FitThreshold` (default 70, bound with options). At 70 a candidate who meets all
  must-haves but no nice-to-haves of a 3+2 posting scores 75 (strong), and one missing a must-have of a
  2+1 posting scores 60 (weak).
- **Requirement count.** Fit scoring accepts 1–50 requirements; the workflow returns 422 when extraction
  yields 0 or more than 50 instead of truncating silently.
- **Study plan.** `StudyPlanner` calls `IChatClient.GetResponseAsync<StudyPlanOutput>` (JSON schema,
  temperature 0) with the missing/partial requirements (id, description, importance, judge rationale) inside
  `<requirement id="…">` delimiters, and asks for `focus` (what to learn) and 2–4 concrete `steps`. The code
  grounds the output: unknown or non-gap ids dropped, duplicates removed, must-have first. No call when
  there are no gaps. It does not cite CV evidence: the plan is about what the evidence lacks.
- **Telemetry.** `WithOpenTelemetry()` on the builder; ServiceDefaults adds the
  `Microsoft.Agents.AI.Workflows` activity source. Sensitive data stays disabled (no message contents).

## Risks / Trade-offs

- Agent Framework moves fast (monthly minors); the workflow is isolated in `Analysis/` behind a small
  `JobAnalysisWorkflow` class to contain API changes.
- A fixed threshold is a product decision, not a learned one; it is configurable and the full fit result
  is always returned so the client can decide differently.
- Synchronous runs on CPU Ollama take minutes (extraction + one judge call per requirement + plan); fine for
  a demo, and stage 6 introduces resumable runs.
