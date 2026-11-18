## MODIFIED Requirements

### Requirement: Cover letter errors
The system SHALL retry the cover letter writer call once when the model returns a draft with no non-blank sentences,
and SHALL fail the analysis with 502 and no partial result when the writer or the verifier fails or returns output
that does not match the expected structure, including when the retry still produces a draft with no sentences.

#### Scenario: Empty draft retries once and succeeds
- **WHEN** the writer model returns an empty `sentences` list on the first call and a valid draft on the retry
- **THEN** the writer returns the sentences from the retry without failing

#### Scenario: Writer fails
- **WHEN** the writer model call fails or returns invalid output (including empty sentences on both the initial call and retry)
- **THEN** the analysis response is 502

#### Scenario: Verifier fails
- **WHEN** the verifier model call fails or returns invalid output
- **THEN** the analysis response is 502
