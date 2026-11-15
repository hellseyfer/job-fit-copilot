# Design

## Context

Stage 4 built the analysis graph with a terminal `strong-fit` node. Agent Framework workflows run in
supersteps and allow cycles through conditional edges, which is what a bounded writer/verifier loop needs.
The fit result already carries, per assessment, the cited `EvidenceItem`s (document, section, chunk index,
verbatim text), so the letter can be grounded in exactly what the judge accepted.

## Goals / Non-goals

Goals: a letter whose claims are all traceable to cited CV evidence, with the enforcement in code where
possible and an explicit, bounded LLM loop where judgment is needed; a loop that shows up in the graph and
in the traces. Non-goals: style control, a standalone endpoint, human approval (stage 6).

## Decisions

- **Graph.**
  ```
  score-fit ──switch──► write-cover-letter ──► verify-cover-letter ──switch──► write-cover-letter  (rewrite due)
            └─────────► plan-study                                   └──────► finalize-cover-letter (accepted or 3 drafts)
  ```
  The writer → verifier → writer cycle uses `AddSwitch` on `verify-cover-letter` with the case
  `draft.RewriteDue` (some sentence unsupported and fewer than 3 drafts). `finalize-cover-letter` and
  `plan-study` are the output executors; `route` stays `strong-fit` / `weak-fit`.
- **Loop state travels in the message.** `LetterDraft { Stage, Evidence, Attempt, Sentences, Verdicts }` is
  immutable; the verifier returns a new draft with verdicts, the writer receives the previous draft as
  feedback. No shared workflow state, so the loop is a pure function per node and easy to test.
- **Evidence numbering.** Distinct `(documentId, chunkIndex)` items cited by `met` / `partial` assessments,
  in assessment order, numbered from 1. The writer prompt lists the requirements with the numbers of their
  evidence, and the evidence items once each.
- **Structured outputs.** Writer: `{ sentences: [{ text, kind: "claim" | "connective", evidence: int[] }] }`.
  Verifier, per sentence: `{ quote, reason, verdict: "supported" | "unsupported" }`. Both at temperature 0 with JSON schema,
  like the extractor and the judge; reasons come before decisions in the schema for small models.
- **Deterministic grounding first.** Out-of-range numbers are dropped and duplicates removed; a claim left
  without citations is unsupported ("cites no evidence") and is not sent to the verifier, which only sees the
  remaining sentences, each with its own cited evidence. Connective sentences are verified too, so labelling a claim
  as connective does not bypass the check. A missing verdict counts as unsupported (fail closed).
- **One verifier call per sentence** (at most 4 at a time), with only that sentence's cited evidence. A single call
  for the whole draft was tried first: on the labeled verifier eval `qwen2.5:3b` scored 0.42 (every sentence
  `unsupported` with a boolean field, then near-random with a `verdict` string). Small prompts per item are what
  made the fit judge work, and they also make each verdict independent of the rest of the letter. The verdict comes
  after a `quote` from the evidence and a `reason`, so the model looks at the evidence before deciding.
- **Bounded loop: 3 drafts at most** (1 write + 2 rewrites), then unsupported sentences are removed. Each draft
  costs 1 writer call plus one verifier call per sentence (about 6–10); the common case is a single draft. The response reports `attempts` and `removed`.
- **Response.** `coverLetter { text, sentences: [{ text, evidence: EvidenceItem[] }], removed: [{ text, reason }], attempts }`;
  `text` joins the kept sentences with spaces. Absent on `weak-fit` (`JsonIgnore` when null), like `studyPlan`.
- **Errors.** `CoverLetterFailedException` (model error, invalid structure, empty draft) → 502 through the
  existing workflow error mapping.
- **Evals.** The verifier is judged on a labeled golden set (supported / unsupported sentences against fixed
  evidence), so its accuracy is measured independently of the writer. The letter eval runs the whole loop on
  the golden CV and checks that terms the CV lacks (e.g. Rust, Salesforce, PhD) never appear in the letter.

## Risks / Trade-offs

- The verifier is an LLM and can be wrong in both directions; the verifier eval measures it, and the
  deterministic citation check does not depend on it.
- Removing sentences can leave a choppy letter; the client gets `removed` to decide, and stage 6 adds human
  approval.
- Latency and cost grow with the letter length (one verifier call per sentence per draft); acceptable for a demo,
  measured in the end-to-end run.
