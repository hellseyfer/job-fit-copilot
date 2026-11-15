## Purpose
Judges, with cited evidence from the candidate's stored documents, whether the candidate meets each
job requirement, and turns those judgments into an overall fit score with strengths and gaps.

## ADDED Requirements

### Requirement: Fit scoring endpoint
The API SHALL expose `POST /api/fit/score`, accepting `requirements` (each with `id`, `description`
and `importance`, `must-have` or `nice-to-have`; other fields are ignored) and an optional `kind`
(`cv` or `project`), and SHALL retrieve the top 3 evidence items per requirement with the evidence
retrieval capability.

#### Scenario: Requirements from extraction are accepted
- **GIVEN** a stored CV
- **WHEN** a client posts requirements with the extraction fields `category` and `excerpt` included
- **THEN** the response is 200 with one assessment per requirement

### Requirement: Assessment per requirement
Each assessment SHALL contain the requirement `id`, `description` and `importance`, a `status`
(`met`, `partial` or `missing`), a short `rationale` and the cited evidence, and assessments SHALL be
in request order.

#### Scenario: Supported and unsupported requirements
- **GIVEN** a stored CV that describes PostgreSQL experience and does not mention Rust
- **AND** the judge model answers `met` citing the PostgreSQL chunk, and `missing` for Rust
- **WHEN** a client posts both requirements to `/api/fit/score`
- **THEN** the response is 200 with two assessments in request order
- **AND** the PostgreSQL assessment is `met` and cites the PostgreSQL chunk
- **AND** the Rust assessment is `missing` with no citations

#### Scenario: Kind filter is applied to the evidence
- **GIVEN** a stored CV and a stored project
- **WHEN** a client scores a requirement with `kind` `project`
- **THEN** every cited evidence item has kind `project`

### Requirement: Judgment by the language model
For each requirement with at least one evidence item, the API SHALL call the configured chat model
once, with temperature 0 and structured output, passing the requirement and its evidence numbered
from 1 as untrusted delimited data, and asking for `status`, `rationale` and the numbers of the
evidence items it relied on.

#### Scenario: One judgment call per requirement with evidence
- **GIVEN** a stored CV
- **WHEN** a client scores three requirements
- **THEN** the chat model is called three times

#### Scenario: Requirement without evidence is missing without calling the model
- **GIVEN** an empty knowledge store
- **WHEN** a client scores a requirement
- **THEN** the assessment is `missing` with no citations
- **AND** the chat model is not called

### Requirement: Citations are grounded in the retrieved evidence
Each cited evidence item SHALL be one of the items retrieved for that requirement, returned with its
`documentId`, `kind`, `title`, `section`, `chunkIndex` and verbatim `text`. Cited numbers outside the
provided range SHALL be dropped, and a `met` or `partial` status left without any valid citation
SHALL become `missing`.

#### Scenario: Citation numbers that were not provided are dropped
- **GIVEN** the judge answers `met` citing evidence 1 and evidence 9 when 3 items were provided
- **WHEN** the requirement is scored
- **THEN** the assessment cites only evidence 1

#### Scenario: Positive status without valid citations becomes missing
- **GIVEN** the judge answers `met` citing only evidence 9 when 3 items were provided
- **WHEN** the requirement is scored
- **THEN** the assessment is `missing` with no citations

### Requirement: Overall fit score
The response SHALL include a `score` from 0 to 100 computed in code as the weighted average of the
statuses (`met` = 1, `partial` = 0.5, `missing` = 0; weight 2 for `must-have` and 1 for
`nice-to-have`) rounded to the nearest integer, and the ids of the requirements that are `met`
(`strengths`), `partial` (`partial`) and `missing` (`gaps`).

#### Scenario: Weighted score
- **GIVEN** a must-have requirement judged `met`, a must-have judged `partial` and a nice-to-have
  judged `missing`
- **WHEN** they are scored
- **THEN** the score is 60
- **AND** `strengths`, `partial` and `gaps` contain one id each

### Requirement: Invalid input is rejected before calling any model
The API SHALL return 400 with validation problem details, without calling the embedding or chat
model, when `requirements` is missing, empty or has more than 50 items, when an `id` is missing or
repeated, when a `description` is empty or longer than 1,000 characters, when `importance` is not
`must-have` or `nice-to-have`, or when `kind` is not `cv` or `project`.

#### Scenario: Invalid importance
- **WHEN** a client posts a requirement with importance `optional`
- **THEN** the response is 400
- **AND** neither model is called

#### Scenario: Invalid evidence parameters
- **WHEN** a client posts no requirements, a duplicate id, an empty description or kind `letter`
- **THEN** the response is 400
- **AND** neither model is called

### Requirement: Model failures
The API SHALL return 502 with problem details and no partial result when the embedding model fails,
when the chat model fails, or when the chat model output does not match the expected structure.

#### Scenario: Chat model error
- **GIVEN** a stored CV and a chat model that throws
- **WHEN** a client scores a requirement
- **THEN** the response is 502

#### Scenario: Invalid judge output
- **GIVEN** a stored CV and a chat model that returns text that is not the expected JSON
- **WHEN** a client scores a requirement
- **THEN** the response is 502

#### Scenario: Embedding model error
- **GIVEN** an embedding model that throws
- **WHEN** a client scores a requirement
- **THEN** the response is 502
- **AND** the chat model is not called

### Requirement: Judge calls are traced
Every judgment call SHALL produce an OpenTelemetry chat span with the model name and token usage,
through the same pipeline as requirement extraction.

#### Scenario: Judgment span
- **GIVEN** a stored CV
- **WHEN** a client scores a requirement
- **THEN** a chat span is recorded with the chat model name and input and output token counts

### Requirement: Fit judgment quality is evaluated
A fit eval SHALL run against the real chat and embedding models with a golden CV and requirements
labeled as supported or unsupported by the CV, and SHALL measure judgment accuracy: the share of
requirements whose status agrees with the label (`met` or `partial` for supported, `missing` for
unsupported).

#### Scenario: Accuracy on the golden set
- **GIVEN** the golden CV is ingested with the real embedding model
- **WHEN** the eval scores the golden requirements with the real chat model
- **THEN** the judgment accuracy is at least 0.8
