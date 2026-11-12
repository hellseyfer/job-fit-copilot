# Spec Delta

## Purpose

Turns the free text of a job posting into a structured list of requirements, each one grounded in the
posting, so that later stages can match and score a candidate requirement by requirement.

## ADDED Requirements

### Requirement: Extract structured requirements from a job posting
The API SHALL expose `POST /api/requirements/extract` that accepts a JSON body with a `jobPosting`
text and responds 200 with the job `title`, the `seniority` (`junior`, `mid`, `senior`, `lead` or
`unspecified`) and a list of `requirements`, each with a stable `id`, a `description`, a `category`
(`skill`, `experience`, `education`, `language`, `certification` or `other`) and an `excerpt`.

#### Scenario: Posting with several requirements
- **WHEN** a client posts a job posting that asks for "5+ years of C#" and "experience with Docker"
- **THEN** the response status is 200
- **AND** the response contains one requirement for each of them, with its category and excerpt

#### Scenario: Requirement ids are unique
- **WHEN** an extraction returns more than one requirement
- **THEN** every requirement has a different `id`

### Requirement: Classify requirement importance
Each extracted requirement SHALL have an `importance` of `must-have` or `nice-to-have`. Requirements
the posting marks as optional, preferred, desirable or a plus SHALL be `nice-to-have`; all others
SHALL be `must-have`.

#### Scenario: Required and optional requirements
- **WHEN** a posting says "Required: Kubernetes" and "Nice to have: Terraform"
- **THEN** the Kubernetes requirement has importance `must-have`
- **AND** the Terraform requirement has importance `nice-to-have`

### Requirement: Requirements are grounded in the posting
Every returned requirement SHALL include an `excerpt` that appears verbatim in the posting, ignoring
case and differences in whitespace. Requirements whose excerpt is not found in the posting SHALL be
removed from the response.

#### Scenario: Model returns an ungrounded requirement
- **WHEN** the model returns a requirement whose excerpt is not part of the posting
- **THEN** that requirement is not included in the response
- **AND** the grounded requirements are still returned

#### Scenario: Excerpt differs only in case and whitespace
- **WHEN** the model returns an excerpt that matches the posting except for letter case and line breaks
- **THEN** the requirement is included in the response

### Requirement: Validate the job posting input
The API SHALL respond 400 with a problem details body, without calling the model, when `jobPosting`
is missing, empty or whitespace, or longer than 20,000 characters.

#### Scenario: Empty posting
- **WHEN** a client posts an empty `jobPosting`
- **THEN** the response status is 400
- **AND** the model is not called

#### Scenario: Oversized posting
- **WHEN** a client posts a `jobPosting` longer than 20,000 characters
- **THEN** the response status is 400
- **AND** the model is not called

### Requirement: Report model failures without partial results
The API SHALL respond 502 with a problem details body when the model call fails or its output cannot
be parsed into the expected structure, and SHALL NOT return partial results.

#### Scenario: Model call fails
- **WHEN** the model provider returns an error
- **THEN** the response status is 502

#### Scenario: Model output is not valid
- **WHEN** the model returns output that does not match the expected structure
- **THEN** the response status is 502

### Requirement: Postings without requirements
The API SHALL respond 200 with an empty `requirements` list when the posting contains no
identifiable requirements.

#### Scenario: Text without requirements
- **WHEN** a client posts text that contains no requirements
- **THEN** the response status is 200
- **AND** the `requirements` list is empty

### Requirement: Model calls are traced
Every model call made during an extraction SHALL produce an OpenTelemetry span that records the
model name and, when the provider reports them, the input and output token counts.

#### Scenario: Extraction trace
- **WHEN** an extraction completes
- **THEN** a span for the model call is recorded with the model name and token usage
