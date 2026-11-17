# Spec Delta

## MODIFIED Requirements

### Requirement: Chunks preserve their source and section
Every chunk SHALL have a zero-based `index` unique within its document, the `section` (the path of
Markdown headings that contain it, empty when there are none) and its `text`, a verbatim part of the
content after contact data is masked. `GET /api/knowledge/documents/{id}/chunks` SHALL return the chunks in order,
without vectors.

#### Scenario: Markdown sections become chunk sections
- **WHEN** a CV has a `# Experience` heading with a `## Acme` sub-heading followed by a paragraph
- **THEN** the chunk containing that paragraph has section `Experience > Acme`

#### Scenario: Unknown document chunks
- **WHEN** a client requests the chunks of a document id that does not exist
- **THEN** the response status is 404

#### Scenario: Chunks hold masked contact data
- **WHEN** a CV contains an email address
- **THEN** the chunk that contained it has `[email]` in its place
