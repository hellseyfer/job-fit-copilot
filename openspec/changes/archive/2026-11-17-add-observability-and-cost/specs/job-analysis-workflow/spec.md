# Spec Delta

## MODIFIED Requirements

### Requirement: Job analysis endpoint
The system SHALL expose `POST /api/analyses` that accepts a `jobPosting` (required, at most 20,000
characters) and an optional `kind` (`cv` or `project`), runs the job analysis workflow and returns the
`runId`, `status`, posting (`title`, `seniority`, `requirements`), `fit`, `route`, the executed `path`, `usage`
(`inputTokens`, `outputTokens`, `totalTokens`, `costUsd`, `durationMs`, `modelCalls`), and a `coverLetter` (strong
fit, 202 awaiting approval) or `studyPlan` (weak fit, 200 completed).

#### Scenario: Strong fit
- **WHEN** a job posting is analyzed and the fit score is greater than or equal to the configured threshold
- **THEN** the response is 202 with `route` `strong-fit`, `status` `awaiting-approval`, a `coverLetter`, no `studyPlan`, `usage` summary, and `path` `["extract-requirements", "score-fit", "write-cover-letter", "verify-cover-letter", "finalize-cover-letter"]` when the first draft is accepted, and the study-plan model is not called

#### Scenario: Weak fit
- **WHEN** a job posting is analyzed and the fit score is below the configured threshold
- **THEN** the response is 200 with `route` `weak-fit`, `status` `completed`, `path` `["extract-requirements", "score-fit", "plan-study"]`, a `studyPlan`, `usage` summary and no `coverLetter`, and the cover-letter models are not called

#### Scenario: Kind is applied to retrieval
- **WHEN** an analysis is requested with `kind` `project`
- **THEN** the fit assessments cite only evidence from `project` documents

#### Scenario: Invalid input
- **WHEN** the `jobPosting` is empty, longer than 20,000 characters, or `kind` is not `cv` or `project`
- **THEN** the response is 400 with validation problem details and no model is called
