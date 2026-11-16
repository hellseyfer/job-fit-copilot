## ADDED Requirements

### Requirement: Pause for approval
On the `strong-fit` route the system SHALL pause the workflow after `finalize-cover-letter` at the
`review-cover-letter` request port, persist the run and its checkpoint in PostgreSQL, and return 202 with
`runId`, `status` `awaiting-approval` and the result so far, including the `coverLetter`.

#### Scenario: Strong fit waits for approval
- **WHEN** a job posting is analyzed with a strong fit
- **THEN** the response is 202 with a `runId`, `status` `awaiting-approval`, the `coverLetter` and a `Location` header for the run

#### Scenario: Run status
- **WHEN** `GET /api/analyses/{runId}` is called for a run awaiting approval
- **THEN** the response is 200 with the same `status` and result

#### Scenario: Unknown run
- **WHEN** `GET /api/analyses/{runId}` or `POST /api/analyses/{runId}/approval` is called with an unknown id
- **THEN** the response is 404

### Requirement: Approval decision
The system SHALL expose `POST /api/analyses/{runId}/approval` with `approved` (required) and an optional
`feedback` (at most 2,000 characters), resume the run from its checkpoint with that decision, and return 200
with `status` `approved` when approved, or `rejected` when rejected without feedback.

#### Scenario: Approve
- **WHEN** a run awaiting approval is approved
- **THEN** the response is 200 with `status` `approved`, the same `coverLetter`, and `path` ending in `review-cover-letter`, `apply-review`

#### Scenario: Reject without feedback
- **WHEN** a run awaiting approval is rejected without feedback
- **THEN** the response is 200 with `status` `rejected` and the cover-letter writer is not called again

#### Scenario: Invalid decision
- **WHEN** the body has no `approved` value or a `feedback` longer than 2,000 characters
- **THEN** the response is 400 and the run is still awaiting approval

### Requirement: Rejection with feedback
When a run is rejected with feedback the system SHALL rewrite the letter with that feedback as an extra
instruction, verify it with the same loop, pause again and return 202 `awaiting-approval` with the new letter.
After 3 rejections with feedback the system SHALL treat a further rejection as final (`rejected`).

#### Scenario: Feedback produces a new letter
- **WHEN** a run is rejected with feedback "Mention the PostgreSQL work first"
- **THEN** the response is 202 `awaiting-approval` with a new `coverLetter`, and the writer prompt contained the feedback as untrusted data

#### Scenario: Rejection limit
- **WHEN** a run has already been rejected with feedback 3 times and is rejected with feedback again
- **THEN** the response is 200 with `status` `rejected` and the writer is not called

#### Scenario: Model failure while resuming
- **WHEN** the writer or verifier fails while a rejection with feedback is applied
- **THEN** the response is 502, the run is `awaiting-approval` again and the decision can be retried

### Requirement: Durable and exclusive runs
The system SHALL resume runs from the PostgreSQL checkpoint, so a run survives an API restart, and SHALL accept
only one decision per pause: a decision for a run that is not awaiting approval returns 409.

#### Scenario: Resume after restart
- **WHEN** the API restarts between the analysis and the approval
- **THEN** the approval resumes the run and returns `status` `approved`

#### Scenario: Interrupted resume
- **WHEN** the API stops while a decision is being applied and a new decision arrives more than 15 minutes later
- **THEN** the run is resumed from its last checkpoint as if it were awaiting approval

#### Scenario: Run already decided
- **WHEN** a decision is sent for a run whose status is `approved`, `rejected` or `completed`
- **THEN** the response is 409 and the run does not change

#### Scenario: Concurrent decisions
- **WHEN** two decisions for the same paused run arrive at the same time
- **THEN** one is applied and the other returns 409
