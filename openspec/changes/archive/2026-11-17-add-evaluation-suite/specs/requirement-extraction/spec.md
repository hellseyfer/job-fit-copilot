# Spec Delta

## ADDED Requirements

### Requirement: Extraction quality is evaluated
An extraction eval SHALL run against the real chat model over a golden set of job postings, each labeled with its
expected must-have requirement keywords, and SHALL measure must-have recall: the share of expected must-have
keywords matched in extracted `must-have` requirements.

#### Scenario: Must-have recall on the golden postings
- **WHEN** the extraction eval runs against the configured chat model on each golden job posting
- **THEN** must-have recall is at least 0.75 for each posting
