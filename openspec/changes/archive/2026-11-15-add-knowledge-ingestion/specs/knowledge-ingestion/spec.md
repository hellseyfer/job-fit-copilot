# Spec Delta

## Purpose
Stores the candidate's CV and project descriptions as embedded, section-aware chunks in a vector
database, so that later stages can retrieve and cite them as evidence for each job requirement.

## ADDED Requirements

### Requirement: Ingest a knowledge document
The API SHALL expose `PUT /api/knowledge/documents/{id}` that accepts a `kind` (`cv` or `project`),
a `title` and a plain-text or Markdown `content`, splits the content into chunks, embeds every chunk
and stores the document with its chunks. The response SHALL contain the `id`, `kind`, `title`,
`chunkCount`, `embeddingModel` and a `status` (`created`, `updated` or `unchanged`), with status
code 201 for a new document and 200 otherwise.

#### Scenario: New document is ingested
- **WHEN** a client puts a CV with id `cv` that did not exist before
- **THEN** the response status is 201 with status `created` and a `chunkCount` greater than 0
- **AND** requesting the document's chunks returns that number of chunks, each one stored with an embedding

### Requirement: Chunks preserve their source and section
Every chunk SHALL have a zero-based `index` unique within its document, the `section` (the path of
Markdown headings that contain it, empty when there are none) and its `text`, a verbatim part of the
content. `GET /api/knowledge/documents/{id}/chunks` SHALL return the chunks in order, without vectors.

#### Scenario: Markdown sections become chunk sections
- **WHEN** a CV has a `# Experience` heading with a `## Acme` sub-heading followed by a paragraph
- **THEN** the chunk containing that paragraph has section `Experience > Acme`

#### Scenario: Unknown document chunks
- **WHEN** a client requests the chunks of a document id that does not exist
- **THEN** the response status is 404

### Requirement: Chunks are bounded in size
Chunks SHALL be at most 1,500 characters and follow Markdown headings and paragraph boundaries when
possible. A paragraph longer than the limit SHALL be split at word boundaries, with overlap between
consecutive pieces.

#### Scenario: Long paragraph is split with overlap
- **WHEN** a document contains a single paragraph of 4,000 characters
- **THEN** it is stored as more than one chunk, none longer than 1,500 characters
- **AND** consecutive chunks share overlapping text and every chunk's text appears verbatim in the content

### Requirement: Re-ingestion replaces a document
Putting a document with an existing id SHALL replace its kind, title, content and all its chunks, so
no chunk of the previous version remains. When kind, title, content and embedding model are identical
to the stored document, the API SHALL respond with status `unchanged` without calling the embedding
model.

#### Scenario: Changed content replaces chunks
- **WHEN** a client puts new content for an existing document id
- **THEN** the response status is 200 with status `updated`
- **AND** the stored chunks are only those of the new content

#### Scenario: Identical content is not embedded again
- **WHEN** a client puts exactly the same document twice
- **THEN** the second response is 200 with status `unchanged`
- **AND** the embedding model is called only for the first request

### Requirement: Delete a knowledge document
The API SHALL expose `DELETE /api/knowledge/documents/{id}` that removes the document and all its
chunks and responds 204, or 404 when the document does not exist.

#### Scenario: Existing document is deleted
- **WHEN** a client deletes an ingested document
- **THEN** the response status is 204
- **AND** requesting its chunks returns 404

#### Scenario: Unknown document is deleted
- **WHEN** a client deletes a document id that does not exist
- **THEN** the response status is 404

### Requirement: Validate documents before embedding
The API SHALL respond 400 with problem details, without calling the embedding model and without
changing stored data, when the id does not match `^[a-z0-9][a-z0-9-]{0,63}$`, the `kind` is not
`cv` or `project`, the `title` is empty or longer than 200 characters, or the `content` is empty,
whitespace only or longer than 50,000 characters.

#### Scenario: Empty content
- **WHEN** a client puts a document whose content is empty or whitespace only
- **THEN** the response status is 400 and the embedding model is not called

#### Scenario: Oversized content
- **WHEN** a client puts a document whose content has more than 50,000 characters
- **THEN** the response status is 400 and the embedding model is not called

#### Scenario: Invalid id or kind
- **WHEN** a client puts a document with id `My CV!` or with kind `letter`
- **THEN** the response status is 400 and the embedding model is not called

### Requirement: Embedding failures leave stored data unchanged
When the embedding model fails or returns vectors whose number or dimensions do not match the
chunks, the API SHALL respond 502 with problem details and SHALL keep the previously stored version
of the document, if any, unchanged.

#### Scenario: Provider error
- **WHEN** the embedding model throws an error while ingesting a new version of an existing document
- **THEN** the response status is 502
- **AND** the document's stored chunks are those of the previous version

#### Scenario: Wrong embedding dimensions
- **WHEN** the embedding model returns vectors with a different number of dimensions than the store expects
- **THEN** the response status is 502 and nothing is stored for that document

### Requirement: Embedding calls are traced
Each embedding call SHALL produce an OpenTelemetry span that records the model name and, when the
provider reports it, the input token usage. Document content SHALL NOT be recorded in telemetry.

#### Scenario: Embedding span
- **WHEN** a document is ingested
- **THEN** a span for the embedding call is recorded with the embedding model name and input token count
- **AND** the span does not contain the document text
