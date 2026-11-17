# Spec Delta

## MODIFIED Requirements

### Requirement: CI quality gate runs the evaluation suite
The CI configuration SHALL include a dedicated manual (`workflow_dispatch`) evaluation workflow that runs the
evaluation suite against a real language and embedding model with `JOBFIT_EVAL_REQUIRED=true`, publishes the
Markdown summary and JSON report artifact, and fails the workflow if any eval fails or is skipped.

#### Scenario: CI quality gate execution
- **WHEN** the `Evals` workflow is triggered manually via `workflow_dispatch`
- **THEN** it executes `JobFit.Evals` with `JOBFIT_EVAL_REQUIRED=true` and `JOBFIT_EVAL_REPORT` set, and fails the job if any threshold is not met
