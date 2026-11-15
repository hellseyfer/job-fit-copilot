# Design

## Context

Stage 1 extracts requirements (`id`, `description`, `category`, `importance`, `excerpt`) with
`IChatClient` structured output at temperature 0, behind the shared OpenTelemetry + logging pipeline
and raised HTTP timeouts. Stage 3 `EvidenceRetriever.SearchAsync` embeds all requirement
descriptions in one call and returns the top-k chunks per requirement with citation fields and a
cosine `score`, filtered by embedding model and optional `kind`. See proposal.md for motivation.

## Goals / Non-Goals

**Goals:**
- One request turns extracted requirements into a cited, explainable assessment and a fit score.
- The model only judges; ids, citations and the score are produced or checked by code.
- Judgment quality is measurable (eval) and every rule is testable with fake models.

**Non-Goals:**
- Choosing the study-plan / cover-letter threshold (stage 4 graph).
- Judging requirements against the whole CV instead of the retrieved evidence.

## Decisions

- **`POST /api/fit/score` runs retrieval itself.** Body: `{ requirements: [{ id, description,
  importance }], kind? }`, the output of `/api/requirements/extract` with extra fields ignored.
  Evidence `top` is fixed at 3 (the eval-backed recall@3). Alternative: accept evidence from the
  client; it would let callers inject fake evidence and duplicates the chaining the graph will do.
- **One judge call per requirement, bounded concurrency (4).** Each prompt holds one requirement and
  at most 3 chunks (≤ 4,500 characters), so prompts stay small for local models, a failure is
  attributable, and the structured output is trivial. Alternative: one call for all requirements;
  fewer calls but prompts grow with 50 × 3 chunks and one bad item invalidates the whole output.
  The concurrency limit protects Ollama on CPU and provider rate limits. Requirements with no
  evidence skip the call.
- **Structured output `JudgedRequirement { status, rationale, evidence: int[] }`** with
  `useJsonSchemaResponseFormat: true` and temperature 0, like the extractor. The system prompt
  defines the statuses (`met`: evidence clearly shows it; `partial`: related or weaker experience;
  `missing`: evidence does not show it), forbids using knowledge outside the evidence and treats the
  `<requirement>` and `<evidence n="…">` blocks as untrusted data.
- **Deterministic grounding.** Citation numbers outside 1..n are dropped and duplicates removed; a
  `met`/`partial` without valid citations becomes `missing` and the rationale is kept. Citations are
  mapped back to the retrieved chunks, so returned text is always verbatim stored text.
- **Score in code.** `score = round(100 × Σ wᵢ·vᵢ / Σ wᵢ)` with `v` met 1 / partial 0.5 / missing 0
  and `w` must-have 2 / nice-to-have 1 (`MidpointRounding.AwayFromZero`). A score the model invents
  could not be explained or tested; weights live in one `FitScorer` class.
- **Errors.** Validation reuses the evidence rules (moved to a shared validator) plus `importance`.
  `EmbeddingFailedException` → 502; any chat exception or unparseable output → `JudgeFailedException`
  → 502. A failure cancels the remaining judge calls; no partial response.
- **Feature folder `Fit/`**: `Contracts.cs`, `FitEndpoints.cs`, `FitJudge.cs` (prompt, call,
  grounding), `FitScorer.cs` (pure function), `FitScoringService.cs` (retrieve → judge → score).
- **Tests.**
  - `FakeChatClient` gets a `Responding(Func<IEnumerable<ChatMessage>, string>)` factory so a test
    can answer per requirement (e.g. `met` with evidence 1 if the prompt mentions PostgreSQL).
  - `JobFit.Api.Tests`: `FitScorer` unit tests (weighted score, empty list), grounding unit tests
    (out-of-range citation dropped, positive status without citations becomes missing), validation
    400s with no model calls, embedding error 502 without chat calls (no database needed because the
    embedding call happens first).
  - `JobFit.IntegrationTests` (pgvector, fake models): end-to-end met/missing with citations and
    request order, kind filter, one judge call per requirement, empty store without chat calls, chat
    error and invalid output 502, chat span with model and token counts.
  - `JobFit.Evals`: golden CV from the retrieval eval plus requirements labeled supported or
    unsupported (e.g. Rust, Salesforce, a medical license); in-memory collection, real chat and
    embedding models; accuracy ≥ 0.8; skipped without `OPENAI_API_KEY` / `OLLAMA_ENDPOINT`.

## Risks / Trade-offs

- [LLM judges are lenient or inconsistent, especially small local models] → Temperature 0,
  citation-required grounding, and the eval measures accuracy including unsupported requirements;
  results with `qwen2.5:3b` are reported, not taken as the quality bar.
- [Retrieval misses the supporting chunk (recall@3 was 0.90)] → The requirement is judged `missing`
  and shows up as a gap with its rationale; improving retrieval stays a separate change.
- [Up to 50 chat calls per request: cost and latency] → gpt-4o-mini with ≤ 3 small chunks per call;
  requirements without evidence are skipped; concurrency 4. Cost reporting comes in stage 9.
- [Prompt injection from a CV chunk] → Evidence is delimited and declared untrusted; the output
  schema only allows a status, a rationale and numbers; full guardrails come in stage 7.

## Migration Plan

No schema change and no new infrastructure.
