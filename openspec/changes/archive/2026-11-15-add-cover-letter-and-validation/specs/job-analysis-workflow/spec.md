## MODIFIED Requirements

### Requirement: Job analysis endpoint
The system SHALL expose `POST /api/analyses` that accepts a `jobPosting` (required, at most 20,000
characters) and an optional `kind` (`cv` or `project`), runs the job analysis workflow and returns the
extracted posting (`title`, `seniority`, `requirements`), the `fit` result with the same shape as
`POST /api/fit/score`, the `route` taken (`strong-fit` or `weak-fit`), the executed node `path`, a
`coverLetter` present only on the `strong-fit` route, and a `studyPlan` present only on the `weak-fit` route.

#### Scenario: Strong fit
- **WHEN** a job posting is analyzed and the fit score is greater than or equal to the configured threshold
- **THEN** the response is 200 with `route` `strong-fit`, a `coverLetter`, no `studyPlan`, and `path` `["extract-requirements", "score-fit", "write-cover-letter", "verify-cover-letter", "finalize-cover-letter"]` when the first draft is accepted, and the study-plan model is not called

#### Scenario: Weak fit
- **WHEN** a job posting is analyzed and the fit score is below the configured threshold
- **THEN** the response is 200 with `route` `weak-fit`, `path` `["extract-requirements", "score-fit", "plan-study"]`, a `studyPlan` and no `coverLetter`, and the cover-letter models are not called

#### Scenario: Kind is applied to retrieval
- **WHEN** an analysis is requested with `kind` `project`
- **THEN** the fit assessments cite only evidence from `project` documents

#### Scenario: Invalid input
- **WHEN** the `jobPosting` is empty, longer than 20,000 characters, or `kind` is not `cv` or `project`
- **THEN** the response is 400 with validation problem details and no model is called

### Requirement: Workflow graph
The system SHALL implement the analysis as a Microsoft Agent Framework workflow whose executors are
`extract-requirements`, `score-fit`, `write-cover-letter`, `verify-cover-letter`, `finalize-cover-letter` and
`plan-study`, routing from `score-fit` to `write-cover-letter` when the score is greater than or equal to
`Workflow:FitThreshold` (default 70) and to `plan-study` otherwise, and looping from `verify-cover-letter`
back to `write-cover-letter` while a rewrite is due.

#### Scenario: Threshold is configurable
- **WHEN** `Workflow:FitThreshold` is set to 50 and the fit score is 60
- **THEN** the route is `strong-fit`

#### Scenario: Score equal to the threshold
- **WHEN** the fit score equals the threshold
- **THEN** the route is `strong-fit`

#### Scenario: Rewrite loop in the path
- **WHEN** the first cover-letter draft is rejected and the second is accepted
- **THEN** the `path` contains `write-cover-letter` and `verify-cover-letter` twice, in order, before `finalize-cover-letter`

#### Scenario: No scorable requirements
- **WHEN** extraction returns no requirements, or more than 50
- **THEN** the response is 422 with problem details and neither the embedding model nor the judge is called
