# job-analysis-workflow Specification

## Purpose
Analyzes a job posting end to end against the candidate's stored documents as a graph workflow: it extracts the
requirements, scores the fit and branches on the score, producing a grounded study plan when the fit is weak.

## Requirements

### Requirement: Job analysis endpoint
The system SHALL expose `POST /api/analyses` that accepts a `jobPosting` (required, at most 20,000
characters) and an optional `kind` (`cv` or `project`), runs the job analysis workflow and returns the
`runId`, `status`, posting (`title`, `seniority`, `requirements`), `fit`, `route`, the executed `path`, and a
`coverLetter` (strong fit, 202 awaiting approval) or `studyPlan` (weak fit, 200 completed).

#### Scenario: Strong fit
- **WHEN** a job posting is analyzed and the fit score is greater than or equal to the configured threshold
- **THEN** the response is 202 with `route` `strong-fit`, `status` `awaiting-approval`, a `coverLetter`, no `studyPlan`, and `path` `["extract-requirements", "score-fit", "write-cover-letter", "verify-cover-letter", "finalize-cover-letter"]` when the first draft is accepted, and the study-plan model is not called

#### Scenario: Weak fit
- **WHEN** a job posting is analyzed and the fit score is below the configured threshold
- **THEN** the response is 200 with `route` `weak-fit`, `status` `completed`, `path` `["extract-requirements", "score-fit", "plan-study"]`, a `studyPlan` and no `coverLetter`, and the cover-letter models are not called

#### Scenario: Kind is applied to retrieval
- **WHEN** an analysis is requested with `kind` `project`
- **THEN** the fit assessments cite only evidence from `project` documents

#### Scenario: Invalid input
- **WHEN** the `jobPosting` is empty, longer than 20,000 characters, or `kind` is not `cv` or `project`
- **THEN** the response is 400 with validation problem details and no model is called

### Requirement: Workflow graph
The system SHALL implement the analysis as a Microsoft Agent Framework workflow with executors
`extract-requirements`, `score-fit`, `write-cover-letter`, `verify-cover-letter`, `finalize-cover-letter`,
`review-cover-letter` (request port), `apply-review` and `plan-study`, routing to `write-cover-letter` when the
score is at least `Workflow:FitThreshold` (default 70) and to `plan-study` otherwise, looping while a rewrite is due.

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

### Requirement: Study plan generation
On the `weak-fit` route the system SHALL ask the chat model, with structured output, for a study plan with
one item per requirement to work on (`requirementId`, `focus`, `steps`), giving it only the requirements
whose status is `missing` or `partial`, with their importance and the judge rationale, as untrusted data
inside delimiters.

#### Scenario: No gaps
- **WHEN** the route is `weak-fit` but no requirement is `missing` or `partial`
- **THEN** the study plan is empty and the study-plan model is not called

#### Scenario: Study plan eval
- **WHEN** the study-plan eval runs against a real model with a posting that the golden CV fits poorly
- **THEN** at least 80% of the gap and partial requirements have a study item

### Requirement: Study plan grounding
The system SHALL drop study items whose `requirementId` is not a `missing` or `partial` requirement, keep
only the first item per requirement, and order items must-have first, then in requirement order.

#### Scenario: Items for other requirements are dropped
- **WHEN** the model returns study items for a gap, for a met requirement and for an unknown id
- **THEN** only the item for the gap is returned

#### Scenario: Must-have gaps first
- **WHEN** the gaps include a nice-to-have requirement listed before a must-have one
- **THEN** the must-have item comes first in the study plan

### Requirement: Workflow errors
The system SHALL return 502 with problem details, and no partial result, when any model call in the
workflow fails or returns output that does not match the expected structure.

#### Scenario: Extraction fails
- **WHEN** the chat model fails during extraction
- **THEN** the response is 502 and the embedding model is not called

#### Scenario: Scoring fails
- **WHEN** the embedding model or the judge fails
- **THEN** the response is 502

#### Scenario: Study plan fails
- **WHEN** the study-plan model fails or returns invalid output
- **THEN** the response is 502

### Requirement: Workflow telemetry
The system SHALL emit an OpenTelemetry span for each workflow run and for each executed node, with the
model call spans of that run nested under the run, without including the job posting or CV text.

#### Scenario: Spans per node
- **WHEN** an analysis completes on the `weak-fit` route
- **THEN** the trace contains a workflow run span and executor spans for `extract-requirements`, `score-fit` and `plan-study`, and the chat spans share the run's trace id
