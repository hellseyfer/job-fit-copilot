# Tasks

## 1. Evaluation harness and report

- [x] 1.1 Add `EvalHarness` (`RequireChatClient`, `RequireEmbeddingGenerator`, `JOBFIT_EVAL_REQUIRED` handling) and `EvalReport` (`Record`, Markdown and JSON output when `JOBFIT_EVAL_REPORT` is set) in `tests/JobFit.Evals/`; verify with unit tests in `tests/JobFit.Api.Tests/EvalSuiteTests.cs` covering required mode vs skip mode and Markdown/JSON report generation
- [x] 1.2 Wire all existing eval classes (`RequirementExtractionEvals`, `EmbeddingEvals`, `EvidenceRetrievalEvals`, `FitScoringEvals`, `StudyPlanEvals`, `CoverLetterEvals`, `GuardrailEvals`) into `EvalHarness`, `EvalReport` and `[Collection("Evals")]`; verify `dotnet build JobFit.slnx` succeeds

## 2. End-to-end golden dataset and eval

- [x] 2.1 Add `tests/JobFit.Evals/golden/end-to-end.json` (golden project document, absent terms, and labeled `strong-fit`, `weak-fit` and `prompt-injection` postings) and `tests/JobFit.Evals/EndToEndEvals.cs`; verify strong-fit, weak-fit and prompt-injection scenarios are covered and `dotnet test tests/JobFit.Api.Tests` passes

## 3. CI quality gate and documentation

- [x] 3.1 Add the `evals` quality-gate job to `.github/workflows/ci.yml` (cached Ollama `qwen2.5:3b` and `nomic-embed-text`, `JOBFIT_EVAL_REQUIRED=true`, `JOBFIT_EVAL_REPORT=$GITHUB_STEP_SUMMARY`, JSON report artifact) and verify the workflow YAML is valid
- [x] 3.2 Update `README.md` (status updated to stage 8, evaluation suite table with golden sets and thresholds, `JOBFIT_EVAL_REQUIRED` and `JOBFIT_EVAL_REPORT` documentation)

## 4. Integration checks

- [x] 4.1 Run `openspec validate --all --strict` and `dotnet test JobFit.slnx` and verify all checks pass

## Workflow follow-up

- Archive the change with `openspec archive add-evaluation-suite` after implementation and review.
