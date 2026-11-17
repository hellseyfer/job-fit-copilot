# evaluation-suite Specification

## Purpose
Evaluates the full analysis pipeline end to end over a golden dataset, reports metric scores against fixed
thresholds in Markdown and JSON, and enforces a quality gate in CI that cannot pass by skipping tests.

## Requirements

### Requirement: End-to-end analysis quality is evaluated on a golden dataset
An eval SHALL ingest the golden CV and project documents into an in-memory vector store with the real embedding
model and run the analysis pipeline (input screening, requirement extraction, fit scoring, route selection, and
cover-letter loop or study planning) with the real chat model over a golden set of job postings.

#### Scenario: Strong-fit golden posting
- **GIVEN** the golden CV and project are ingested with the real embedding model
- **WHEN** the end-to-end eval analyzes a posting labeled `strong-fit`
- **THEN** must-have requirement recall is at least 0.75, the route is `strong-fit`, and the verified cover letter has at least 3 sentences, cites only retrieved evidence and mentions none of the golden terms absent from the candidate's documents

#### Scenario: Weak-fit golden posting
- **GIVEN** the golden CV and project are ingested with the real embedding model
- **WHEN** the end-to-end eval analyzes a posting labeled `weak-fit`
- **THEN** must-have requirement recall is at least 0.75, the route is `weak-fit`, and the study plan covers at least 80% of the `missing` and `partial` requirements

#### Scenario: Injected golden posting
- **WHEN** the end-to-end eval screens a posting labeled `prompt-injection`
- **THEN** the screen rejects the posting before requirement extraction runs

### Requirement: Required evaluation mode prevents silent skips
When `JOBFIT_EVAL_REQUIRED` is `true`, an eval SHALL fail instead of skipping when no chat or embedding model is
configured. When `JOBFIT_EVAL_REQUIRED` is not `true` and no model is configured, the eval SHALL be skipped.

#### Scenario: Missing model in required mode fails
- **GIVEN** neither `OPENAI_API_KEY` nor `OLLAMA_ENDPOINT` is set and `JOBFIT_EVAL_REQUIRED` is `true`
- **WHEN** an eval requests a chat or embedding model
- **THEN** the test fails with an error stating that a model is required

#### Scenario: Missing model in default mode skips
- **GIVEN** neither `OPENAI_API_KEY` nor `OLLAMA_ENDPOINT` is set and `JOBFIT_EVAL_REQUIRED` is unset
- **WHEN** an eval requests a chat or embedding model
- **THEN** the test is skipped

### Requirement: Evaluation report summarizes metrics and thresholds
When `JOBFIT_EVAL_REPORT` is set to a file path, the evaluation suite SHALL write a Markdown summary table of all
recorded metrics (eval name, metric, model, measured value, threshold and pass/fail status) to that path and write
the same entries as JSON to `<path>.json`.

#### Scenario: Markdown and JSON report generation
- **GIVEN** `JOBFIT_EVAL_REPORT` is set to a file path
- **WHEN** evals record their metric results and the report is flushed
- **THEN** the Markdown file contains a table row for each recorded metric with its measured value, threshold and status, and `<path>.json` contains the corresponding JSON array

### Requirement: CI quality gate runs the evaluation suite
The CI configuration SHALL include a dedicated manual (`workflow_dispatch`) evaluation workflow that runs the
evaluation suite against a real language and embedding model with `JOBFIT_EVAL_REQUIRED=true`, publishes the
Markdown summary and JSON report artifact, and fails the workflow if any eval fails or is skipped.

#### Scenario: CI quality gate execution
- **WHEN** the `Evals` workflow is triggered manually via `workflow_dispatch`
- **THEN** it executes `JobFit.Evals` with `JOBFIT_EVAL_REQUIRED=true` and `JOBFIT_EVAL_REPORT` set, and fails the job if any threshold is not met
