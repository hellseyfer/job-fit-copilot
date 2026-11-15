# Spec Delta

## Purpose
Retrieves, for each job requirement, the most relevant chunks of the candidate's stored CV and
projects, with their source and section, so that fit scoring can judge and cite the evidence.

## ADDED Requirements

### Requirement: Search evidence for requirements
The API SHALL expose `POST /api/evidence/search` that accepts `requirements` (each with an `id` and a
`description`), an optional `kind` and an optional `top`, and returns status 200 with the
`embeddingModel` used and one result per requirement, in request order, each with the
`requirementId` and its `evidence` list.

#### Scenario: Relevant chunk is found for a requirement
- **GIVEN** a stored CV with a chunk about PostgreSQL and another about frontend work
- **WHEN** a client searches evidence for the requirement "Experience with PostgreSQL"
- **THEN** the response status is 200 with one result for that requirement id
- **AND** its first evidence item is the PostgreSQL chunk

#### Scenario: Results keep the request order
- **WHEN** a client searches evidence for requirements `req-2` and `req-1`, in that order
- **THEN** the results are for `req-2` and then `req-1`

### Requirement: Evidence items cite their source
Every evidence item SHALL contain the `documentId`, `kind`, `title`, `section`, `chunkIndex`, the
verbatim `text` of the stored chunk and a `score` (cosine similarity, higher is more relevant).
Evidence items of a requirement SHALL be ordered by descending score. Vectors SHALL NOT be returned.

#### Scenario: Evidence is ordered and cites the chunk
- **WHEN** a requirement has several matching chunks
- **THEN** its evidence items are ordered by descending `score`
- **AND** each item's `text` and `section` equal those returned by `GET /api/knowledge/documents/{id}/chunks` for that `chunkIndex`

### Requirement: Number of evidence items is bounded
Each requirement SHALL receive at most `top` evidence items, where `top` is between 1 and 10 and
defaults to 3. A requirement SHALL receive fewer items only when fewer chunks are searchable.

#### Scenario: Default top
- **GIVEN** a stored document with more than 3 chunks
- **WHEN** a client searches without `top`
- **THEN** every requirement has exactly 3 evidence items

#### Scenario: Explicit top
- **GIVEN** a stored document with more than 3 chunks
- **WHEN** a client searches with `top` 1
- **THEN** every requirement has exactly 1 evidence item

### Requirement: Search can be limited to one kind of document
When `kind` is `cv` or `project`, only chunks of documents of that kind SHALL be searched. Without
`kind`, all documents SHALL be searched.

#### Scenario: Kind filter
- **GIVEN** a stored CV and a stored project
- **WHEN** a client searches with `kind` `project`
- **THEN** every evidence item has kind `project`

### Requirement: Only comparable vectors are searched
Only chunks embedded with the currently configured embedding model SHALL be searched, because
vectors from different models are not comparable.

#### Scenario: Chunks from another model are ignored
- **GIVEN** a document was ingested with a different embedding model than the current one
- **WHEN** a client searches evidence
- **THEN** no evidence item comes from that document

#### Scenario: Nothing stored
- **GIVEN** no searchable chunks are stored
- **WHEN** a client searches evidence for a valid requirement
- **THEN** the response status is 200 and the requirement's `evidence` list is empty

### Requirement: Invalid searches are rejected before calling the model
The API SHALL return 400 with problem details, without calling the embedding model, when
`requirements` is missing, empty or has more than 50 items, a requirement `id` is missing or
repeated, a `description` is empty or longer than 1,000 characters, `top` is outside 1–10, or `kind`
is not `cv` or `project`.

#### Scenario: Empty requirements
- **WHEN** a client searches with an empty `requirements` list
- **THEN** the response status is 400 and the embedding model is not called

#### Scenario: Too many requirements
- **WHEN** a client searches with 51 requirements
- **THEN** the response status is 400 and the embedding model is not called

#### Scenario: Missing or duplicate id
- **WHEN** a requirement has no `id`, or two requirements share the same `id`
- **THEN** the response status is 400 and the embedding model is not called

#### Scenario: Invalid description
- **WHEN** a requirement `description` is empty or longer than 1,000 characters
- **THEN** the response status is 400 and the embedding model is not called

#### Scenario: Invalid top or kind
- **WHEN** a client searches with `top` 0, `top` 11 or `kind` `letter`
- **THEN** the response status is 400 and the embedding model is not called

### Requirement: Embedding failures are reported
When the embedding model fails or returns a wrong number of vectors or wrong dimensions, the API
SHALL return 502 with problem details and no partial results.

#### Scenario: Provider error
- **WHEN** the embedding model throws while embedding the requirements
- **THEN** the response status is 502

#### Scenario: Wrong dimensions
- **WHEN** the embedding model returns vectors that do not have 768 dimensions
- **THEN** the response status is 502

### Requirement: Query embeddings are traced
All requirements of a search SHALL be embedded in a single model call, recorded as an OpenTelemetry
span with the model name and input token count.

#### Scenario: One traced embedding call per search
- **WHEN** a client searches evidence for 3 requirements
- **THEN** the embedding model is called once
- **AND** the recorded embedding span has the model name and an input token count

### Requirement: Retrieval quality is evaluated
A retrieval eval SHALL run against the real embedding model with a golden CV and requirements, each
labeled with the section that supports it, and SHALL measure recall@3: the share of supported
requirements whose labeled section appears in their top 3 evidence items.

#### Scenario: Recall at 3 on the golden set
- **GIVEN** the golden CV is ingested with the real embedding model
- **WHEN** the eval searches evidence for the golden requirements
- **THEN** recall@3 is at least 0.8
