# Spec Delta

## Purpose
Provides a browser-based Angular UI for demonstrating CV/project ingestion, job-fit analysis, cover-letter review
and approval, study plans, guardrail feedback, and per-run token/cost usage.

## ADDED Requirements

### Requirement: Candidate documents can be ingested and inspected from the UI
The UI SHALL allow a user to enter or load a plain-text or Markdown CV or project document (`id`, `kind`, `title`,
`content`), submit it to `PUT /api/knowledge/documents/{id}`, and display the resulting ingestion status and chunk
count.

#### Scenario: Ingesting a CV document
- **WHEN** the user submits a CV with `id`, `title` and `content`
- **THEN** the UI calls `PUT /api/knowledge/documents/{id}` and displays the returned status and `chunkCount`

### Requirement: Job analysis runs and displays fit, route, usage and outcome
The UI SHALL allow a user to submit a `jobPosting` and optional `kind` filter to `POST /api/analyses` and SHALL
display the fit `score`, `route`, executed `path`, `usage` (`totalTokens`, `costUsd`, `durationMs`, `modelCalls`),
per-requirement assessments with cited evidence, and either the `studyPlan` or `coverLetter`.

#### Scenario: Weak-fit analysis displays study plan and usage
- **WHEN** `POST /api/analyses` returns 200 with `route` `weak-fit`
- **THEN** the UI renders the score, assessments, `studyPlan` items and `usage` metrics

#### Scenario: Strong-fit analysis displays cover letter and approval controls
- **WHEN** `POST /api/analyses` returns 202 with `status` `awaiting-approval`
- **THEN** the UI renders the `coverLetter` sentences with their cited evidence, any `removed` sentences, and controls to approve, reject, or reject with feedback

### Requirement: Cover letter approval and revision loop works from the UI
When an analysis is `awaiting-approval`, the UI SHALL submit the user's decision to
`POST /api/analyses/{runId}/approval` and update the displayed `status`, `coverLetter`, `path` and accumulated
`usage`.

#### Scenario: Rejecting with feedback updates the draft
- **WHEN** the user submits `approved: false` with non-empty `feedback` and the API returns 202 `awaiting-approval`
- **THEN** the UI displays the revised `coverLetter`, updated `usage` and keeps the approval controls active

#### Scenario: Approving finalizes the run
- **WHEN** the user clicks Approve and the API returns 200 `approved`
- **THEN** the UI displays `approved` status and disables further approval submissions

### Requirement: Guardrail and API errors are surfaced clearly
When any API call returns 400, 422 (including prompt-injection problem details with `field` and `layer`), or 502,
the UI SHALL display an actionable error banner without losing the user's current form input.

#### Scenario: Prompt injection error shows field and layer
- **WHEN** the API returns 422 with `type` `https://job-fit-copilot/problems/prompt-injection`
- **THEN** the UI displays the blocked `field` and `layer`
