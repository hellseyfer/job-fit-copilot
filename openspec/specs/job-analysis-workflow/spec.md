# job-analysis-workflow Specification

## Purpose
TBD - created by archiving change add-workflow-graph. Update Purpose after archive.

## Requirements

### Requirement: Job analysis endpoint
The system SHALL expose `POST /api/analyses` that accepts a `jobPosting` (required, at most 20,000
characters) and an optional `kind` (`cv` or `project`), runs the job analysis workflow and returns the
extracted posting (`title`, `seniority`, `requirements`), the `fit` result with the same shape as
`POST /api/fit/score`, the `route` taken (`strong-fit` or `weak-fit`), the executed node `path`, and a
`studyPlan` that is present only on the `weak-fit` route.

#### Scenario: Strong fit
- **WHEN** a job posting is analyzed and the fit score is greater than or equal to the configured threshold
- **THEN** the response is 200 with `route` `strong-fit`, `path` `["extract-requirements", "score-fit", "strong-fit"]` and no `studyPlan`, and the study-plan model is not called

#### Scenario: Weak fit
- **WHEN** a job posting is analyzed and the fit score is below the configured threshold
- **THEN** the response is 200 with `route` `weak-fit`, `path` `["extract-requirements", "score-fit", "plan-study"]` and a `studyPlan`

#### Scenario: Kind is applied to retrieval
- **WHEN** an analysis is requested with `kind` `project`
- **THEN** the fit assessments cite only evidence from `project` documents

#### Scenario: Invalid input
- **WHEN** the `jobPosting` is empty, longer than 20,000 characters, or `kind` is not `cv` or `project`
- **THEN** the response is 400 with validation problem details and no model is called

### Requirement: Workflow graph
The system SHALL implement the analysis as a Microsoft Agent Framework workflow whose executors are
`extract-requirements`, `score-fit`, `strong-fit` and `plan-study`, reusing the requirement extraction and
fit scoring behavior, and SHALL route from `score-fit` to `strong-fit` when the score is greater than or
equal to `Workflow:FitThreshold` (default 70) and to `plan-study` otherwise.

#### Scenario: Threshold is configurable
- **WHEN** `Workflow:FitThreshold` is set to 50 and the fit score is 60
- **THEN** the route is `strong-fit`

#### Scenario: Score equal to the threshold
- **WHEN** the fit score equals the threshold
- **THEN** the route is `strong-fit`

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
