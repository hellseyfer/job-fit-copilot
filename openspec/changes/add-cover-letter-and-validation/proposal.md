# Proposal

## Why

The `strong-fit` route of the analysis workflow ends without producing anything: the candidate who fits
still has to write the application. A generated cover letter is also the riskiest output of the project,
because an LLM writing persuasive text tends to invent experience. This change adds the letter together
with an enforced anti-hallucination loop, so every claim about the candidate is traceable to CV evidence.

## What Changes

- On the `strong-fit` route the workflow replaces the terminal `strong-fit` node with a loop:
  `write-cover-letter` → `verify-cover-letter` → back to `write-cover-letter` with feedback while sentences
  are unsupported (at most 2 rewrites) → `finalize-cover-letter`.
- The writer gets the posting title and the `met` / `partial` requirements with their cited evidence, numbered,
  and returns the letter as sentences, each marked `claim` or `connective` with the evidence numbers it uses.
- Deterministic checks in code: a `claim` without a valid evidence number is unsupported without asking the
  verifier; citations outside the numbered evidence are dropped.
- An LLM verifier checks each remaining sentence in its own call, with only its cited evidence: a claim must be
  supported by that evidence, and a connective sentence must not state facts about the candidate.
- Sentences still unsupported after the last rewrite are removed from the letter and reported with the reason.
- The analysis response gains a `coverLetter` (text, sentences with their cited evidence, removed sentences,
  attempts), present only on the `strong-fit` route; `route` values and the `weak-fit` route are unchanged.
- New evals: verifier accuracy on labeled sentences, and a real-model letter that must not mention facts
  absent from the golden CV.

## Capabilities

### New Capabilities
- `cover-letter`: generation of a cover letter grounded in cited CV evidence, with code-enforced citations,
  LLM verification and a bounded rewrite loop.

### Modified Capabilities
- `job-analysis-workflow`: the `strong-fit` route runs the cover-letter loop and the response includes
  `coverLetter`; the strong-fit `path` changes.

## Impact

- Code: new `src/JobFit.Api/CoverLetters/` feature folder (`CoverLetterWriter`, `CoverLetterVerifier`,
  contracts, grounding); `JobAnalysisWorkflow` graph and `AnalyzeJobResponse`.
- Tests: API tests for grounding and loop decisions, integration tests for the loop through the graph,
  evals for the verifier and the letter.
- Docs: README endpoint description and graph.
- Cost: a strong fit now costs one writer call plus one verifier call per sentence for each draft (1–3 drafts).

## Out of scope

- A standalone cover-letter endpoint; the letter is only produced by the workflow.
- Tone, length or language options, and editing the letter after it is returned.
- Human approval of the letter before use (stage 6).
- Checking the letter against the job posting for invented company facts; only claims about the candidate
  are verified.
