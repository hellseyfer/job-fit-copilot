## MODIFIED Requirements

### Requirement: Job analysis runs and displays fit, route, usage and outcome
The UI SHALL allow a user to submit a `jobPosting` and optional `kind` filter to `POST /api/analyses`, SHALL clear
any previously displayed analysis result and feedback immediately when a new analysis starts, and SHALL display the
fit `score`, `route`, executed `path`, `usage` (`totalTokens`, `costUsd`, `durationMs`, `modelCalls`),
per-requirement assessments with cited evidence, and either the `studyPlan` or `coverLetter`.

#### Scenario: Weak-fit analysis displays study plan and usage
- **WHEN** `POST /api/analyses` returns 200 with `route` `weak-fit`
- **THEN** the UI renders the score, assessments, `studyPlan` items and `usage` metrics

#### Scenario: Strong-fit analysis displays cover letter and approval controls
- **WHEN** `POST /api/analyses` returns 202 with `status` `awaiting-approval`
- **THEN** the UI renders the `coverLetter` sentences with their cited evidence, any `removed` sentences, and controls to approve, reject, or reject with feedback

#### Scenario: Starting a new analysis clears the current result
- **WHEN** an analysis result is displayed and the user clicks Analyze Fit
- **THEN** the UI immediately clears the displayed analysis result before the new `POST /api/analyses` response arrives

#### Scenario: Copying cover letter omits evidence references
- **WHEN** a `coverLetter` with cited evidence is displayed and the user clicks Copy Cover Letter
- **THEN** the UI copies only the cover letter sentence text to the clipboard without `[documentId#chunkIndex]` or inline `[n]` citation markers
