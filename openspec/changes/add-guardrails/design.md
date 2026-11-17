# Design

## Context

See proposal.md - Why. Five entry points put request text into chat prompts (see the `input-guardrails` spec).
Each prompt already wraps untrusted text in tags (`<job_posting>`, `<evidence>`, `<requirement>`, `<sentence>`,
`<note>`, `<job_title>`, `<previous_draft>`, `<rejected_letter>`, `<reviewer_feedback>`) and the outputs are
grounded and verified, which limits what an injection can achieve but does not stop it. `qwen2.5:3b` on Ollama is the
no-cost model, so the design has to work with a small model and a small context window.

## Goals / Non-Goals

**Goals:**
- One screen, applied the same way at every entry point, before any model or vector-store call.
- Provider-agnostic: the classifier uses the same `IChatClient` as the rest of the API.
- Measurable: the eval reports recall and false positives per layer.

**Non-Goals:**
- A guarantee against injection: the screen is one layer; delimiters, grounding and the verifier stay.
- Entity-based PII detection (names, addresses).

## Decisions

### D1. An `InputGuard` service called by the endpoints, not a graph node
The endpoints call `InputGuard.ScreenAsync(field, text)` after validation. `POST /api/analyses` screens the posting
before the graph runs, so the workflow `path` does not change, and the approval endpoint screens feedback before it
claims the run, so a blocked feedback cannot change it. A blocked text throws `PromptInjectionException(field, layer)`,
which the endpoints map to 422.
*Alternative:* a `screen-input` node in the graph. It would only cover `POST /api/analyses`.

### D2. Patterns first, normalized
Text is normalized before matching (Unicode NFKC, zero-width characters removed, whitespace collapsed, lower case) so
trivial obfuscation does not bypass it. The patterns are deliberately narrow:
- overrides: `ignore|disregard|forget` + `previous|above|prior|all` + `instructions|prompts|rules`; "you are now",
  "new instructions:", "reveal|print your system prompt";
- role markers at a line start (`system:`, `assistant:`) and template tokens (`<|im_start|>`, `<|system|>`, `[INST]`);
- any opening or closing tag in the API's delimiter set (kept in one list next to the prompts).
A narrow pattern that matches has very few false positives; everything else goes to the classifier.

### D3. Classifier: one call per window, rationale before verdict
The text is split on paragraph boundaries into windows of at most 2,000 characters (a CV has 1–3, the largest valid
document 25), judged with at most 4 calls at a time, at temperature 0, with structured output
`{ rationale, verdict }`. Rationale first and one small call per item are the two lessons of stages 3b and 5 with
small models. The prompt states that the text is data to classify, never instructions, and wraps it in
`<untrusted_text>`. Any `injection` verdict blocks. A failed call or a missing/unknown verdict is a 502 (fail closed).
*Alternative:* one call for the whole text. It would exceed the default Ollama context for a long CV.

### D4. Order: validate → mask → patterns → classifier
Masking happens before screening, so the classifier model never sees the contact data either, and the ingestion
content hash is computed on the masked content.

### D5. Masking with regular expressions
- Email: RFC-like `local@domain.tld`.
- URL: `http(s)://…` and `www.…`.
- Phone: an optional `+`, then 8–15 digits with spaces, dots, dashes or parentheses between them. Four-digit year
  ranges (`2020-2025`), percentages and numbers with fewer than 8 digits do not match.
Only knowledge documents are masked (proposal - Out of scope).

### D6. Telemetry without content
A `Meter` `JobFit.Guardrails` with the counter `jobfit.guardrails.blocked` (tags `field`, `layer`), registered in
ServiceDefaults; an `ActivityEvent` `guardrails.blocked` with the same tags on the current span. The chat and
embedding pipelines drop `UseLogging()`: `LoggingChatClient` writes whole messages at `Trace`, and the
OpenTelemetry clients already record each call. `OpenTelemetryChatClient.EnableSensitiveData` stays `false`.

### D7. Problem details
`type` `https://job-fit-copilot/problems/prompt-injection`, `title` "The input looks like a prompt injection.",
extensions `field` and `layer`. The 422 for postings without scorable requirements keeps its default type.

### D8. Tests
`FakeChatClient` answers every call with one JSON, so the API test factory sets `Guardrails:Classifier=false` by
default and the guardrail tests enable it with a fake that answers classifier prompts. Integration tests keep it off
except the blocked-feedback test.

## Risks / Trade-offs

- [False positives on legitimate text, e.g. an LLM-security posting] → patterns are narrow; the eval has benign
  security and feedback cases and a false-positive threshold.
- [Attacks the small model misses] → recall threshold in the eval; the grounding and verification layers remain.
- [Extra latency and cost] → one call per 2,000 characters, parallel, and `Guardrails:Classifier=false` to turn it off.
- [The classifier itself is injectable] → the text is in delimiters, the verdict is an enum, and an invalid answer
  fails closed.
- [Masking a URL a project needs] → accepted; the letter must not contain links anyway.

## Migration Plan

Documents ingested before this change keep unmasked chunks until they are ingested again; the README tells users to
re-ingest. No schema change.
