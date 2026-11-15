## ADDED Requirements

### Requirement: Cover letter writing
The system SHALL write the cover letter with the chat model, using structured output, from the posting title
and the `met` and `partial` requirements with their cited evidence numbered from 1 (each distinct chunk once),
passed as untrusted data inside delimiters. The writer SHALL return the letter as ordered sentences, each with
a `kind` (`claim` about the candidate, or `connective`) and the evidence numbers it relies on.

#### Scenario: Letter from cited evidence
- **WHEN** the route is `strong-fit`
- **THEN** the writer receives only the evidence cited by `met` and `partial` assessments, numbered without duplicates

#### Scenario: Citations are resolved
- **WHEN** a sentence cites evidence numbers 1 and 9 and only 3 items were provided
- **THEN** the sentence keeps evidence 1 with its document, section and text, and number 9 is dropped

### Requirement: Cover letter verification
The system SHALL treat a `claim` with no valid citation as unsupported without asking the verifier, and SHALL
ask the chat model, in one structured call per draft, whether each remaining sentence is supported: a claim
must be backed by its cited evidence, and a connective sentence must state no facts about the candidate. A
sentence the verifier does not judge SHALL count as unsupported.

#### Scenario: Claim without citation
- **WHEN** the writer returns a claim with no valid evidence number
- **THEN** that sentence is unsupported with a reason saying it cites no evidence, and it is not sent to the verifier

#### Scenario: Unsupported claim
- **WHEN** the verifier judges that a claim is not backed by its cited evidence
- **THEN** that sentence is unsupported with the verifier's reason

#### Scenario: Missing verdict
- **WHEN** the verifier output omits the verdict for one sentence
- **THEN** that sentence is unsupported

#### Scenario: Verifier eval
- **WHEN** the verifier eval runs against a real model on labeled sentences and evidence
- **THEN** at least 80% of the sentences are judged as labeled

### Requirement: Rewrite loop
The system SHALL send the unsupported sentences and their reasons back to the writer for a full rewrite while
any sentence is unsupported, at most 2 times, and SHALL then return the last draft without its unsupported
sentences, reporting each removed sentence with its reason and the number of drafts written.

#### Scenario: Letter accepted first time
- **WHEN** every sentence of the first draft is supported
- **THEN** the letter has all its sentences, no removed sentences, `attempts` 1 and the writer is called once

#### Scenario: Rewrite fixes the letter
- **WHEN** the first draft has an unsupported claim and the second draft is fully supported
- **THEN** the letter is the second draft, `attempts` is 2, and the second writer call received the unsupported sentence and its reason

#### Scenario: Unsupported sentences removed after the last rewrite
- **WHEN** the third draft still has an unsupported claim
- **THEN** the writer is not called again, the letter omits that sentence, `removed` lists it with its reason and `attempts` is 3

#### Scenario: Letter eval
- **WHEN** the letter eval runs against a real model with a posting the golden CV fits well
- **THEN** the returned letter has at least 3 sentences and mentions none of the golden facts absent from the CV

### Requirement: Cover letter errors
The system SHALL fail the analysis with 502 and no partial result when the writer or the verifier fails or
returns output that does not match the expected structure, including a draft with no sentences.

#### Scenario: Writer fails
- **WHEN** the writer model call fails or returns invalid output
- **THEN** the analysis response is 502

#### Scenario: Verifier fails
- **WHEN** the verifier model call fails or returns invalid output
- **THEN** the analysis response is 502
