# Proposal

## Why

Stage 3 returns, for every job requirement, the most similar chunks of the candidate's CV and
projects, but a similarity score does not say whether the candidate actually meets the requirement:
"Kubernetes" is always the nearest chunk to something. The copilot needs a judgment per requirement
(met, partial or missing) that cites the evidence it relied on, and one fit score for the posting, so
the next stages can branch (study plan vs. cover letter) and explain why.

## What Changes

- Add `POST /api/fit/score`: receives the requirements returned by `POST /api/requirements/extract`
  (`id`, `description` and `importance` are used, other fields ignored) and an optional `kind`
  filter, retrieves the top 3 evidence chunks per requirement with the stage 3 retriever, and returns
  a per-requirement assessment plus an overall score.
- Judge each requirement with the configured `IChatClient` (LLM as judge): it receives the
  requirement and its numbered evidence and returns, as structured output, `met`, `partial` or
  `missing`, a short rationale and the numbers of the evidence items it relied on. Requirements with
  no evidence are `missing` without calling the model.
- Ground the judgment deterministically: cited numbers that were not provided are dropped, and a
  `met` or `partial` verdict without any valid citation becomes `missing`.
- Compute the score in code, not in the model: `met` = 1, `partial` = 0.5, `missing` = 0, weighted
  2 for must-have and 1 for nice-to-have, as an integer 0–100. Return `strengths` (met), `partial`
  and `gaps` (missing) requirement ids.
- Validate input before calling any model (400); map embedding and chat failures or invalid model
  output to 502 with no partial result. Model calls are traced like the existing ones.
- Add a fit-judgment eval (golden CV with supported and unsupported requirements, real models).

Out of scope: study plan and cover letter (stages 4–5), branching thresholds and the workflow graph
(stage 4), re-ranking, multi-turn or self-consistency judging, caching judgments, cost reporting
(stage 9), authentication and multiple users.

## Capabilities

### New Capabilities
- `fit-scoring`: judging, with cited evidence, how well the candidate meets each job requirement,
  and scoring the overall fit.

### Modified Capabilities
<!-- none -->

## Impact

- New feature folder `Fit/` in `JobFit.Api` (contracts, endpoint, judge, scorer); it reuses
  `EvidenceRetriever`, the `IChatClient` registration from stage 1 and the evidence input validation.
- No schema change and no new infrastructure or packages.
- `FakeChatClient` gains a per-request responder so tests can answer differently per requirement.
- New eval in `JobFit.Evals` (skipped without model credentials).
