# Proposal

## Why

Every text the API sends to a chat model comes from outside: the job posting, the CV and projects, the requirement
descriptions sent to fit scoring, and the reviewer feedback. Today the only defense against prompt injection is the
delimiters in each prompt, and the candidate's contact data (email, phone, links) goes to the model provider. The
roadmap promises prompt-injection and PII guardrails before the system is evaluated and deployed.

## What Changes

- A layered prompt-injection screen runs on every free text that reaches a chat prompt, after input validation and
  before any model or vector-store call:
  1. deterministic patterns: instruction-override phrases, chat role markers and template tokens, and the prompt
     delimiter tags the API uses (for example `</evidence>`);
  2. for text no pattern matched, an LLM classifier on the configured chat model (`IChatClient`), one call per window
     of at most 2,000 characters. `Guardrails:Classifier` (default `true`) turns it off.
- Suspicious input is rejected with **422** (`field` and `layer`, never the text itself) and counted in telemetry.
  **BREAKING** for clients that send text the screen rejects. A blocked reviewer feedback leaves the run awaiting
  approval.
- Emails, phone numbers and URLs in knowledge documents are masked (`[email]`, `[phone]`, `[url]`) before the
  content is screened, chunked, embedded or stored, so no prompt contains them.
- Prompt and completion text stays out of logs and traces at every log level (the chat and embedding pipelines stop
  using `UseLogging`, which writes whole messages at `Trace`).
- A golden set of labeled attacks and benign texts measures the screen's recall and false-positive rate.

## Capabilities

### New Capabilities
- `input-guardrails`: prompt-injection screening of untrusted text, contact-data masking, and keeping request text
  out of telemetry.

### Modified Capabilities
- `knowledge-ingestion`: a chunk's text is a verbatim part of the content after contact data is masked.

## Impact

- Code: new `src/JobFit.Api/Guardrails/` (patterns, classifier, guard, masker, options, telemetry); the extraction,
  knowledge, fit-scoring and analysis endpoints call the guard; ingestion masks the content; the chat and embedding
  pipelines drop `UseLogging`; ServiceDefaults registers the guardrails meter.
- Cost: the classifier adds one model call per 2,000 characters screened (about 1–3 for a posting or a CV).
- Tests: API tests for every entry point, both layers, 422/502 and masking; integration tests for masked stored
  chunks and a blocked feedback on a paused run; an eval with labeled attacks.

## Out of scope

- Masking names, addresses or other PII that needs entity recognition (for example Presidio).
- Masking the job posting: its contact data belongs to the company, and extraction grounding needs it verbatim.
- Screening model output for toxicity or policy violations.
- Authentication, rate limiting and abuse protection.
