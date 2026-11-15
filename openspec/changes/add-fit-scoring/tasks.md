# Tasks

## 1. Scoring

- [ ] 1.1 Move the evidence request validation (requirements, ids, descriptions, kind) to a shared validator used by `/api/evidence/search`; verify the existing evidence tests still pass
- [ ] 1.2 Add `Fit/` contracts and `FitScorer` (weighted 0–100 score, strengths/partial/gaps)
- [ ] 1.3 Add `FitJudge`: prompt with delimited untrusted evidence, structured output at temperature 0, citation grounding, `JudgeFailedException` on errors or invalid output
- [ ] 1.4 Add `FitScoringService` (retrieve top 3 → judge with concurrency 4, skipping requirements without evidence → score) and map `POST /api/fit/score` with 400/502 problem details; add an example to `JobFit.Api.http` and verify it against the AppHost with Ollama

## 2. API tests (fake models, no database)

- [ ] 2.1 Add `FakeChatClient.Responding` (per-request responder); verify the existing tests still pass
- [ ] 2.2 `FitScorer`: met/partial/missing with must-have and nice-to-have weights gives 60; strengths, partial and gaps lists
- [ ] 2.3 Grounding: out-of-range citation numbers are dropped; `met` without valid citations becomes `missing`
- [ ] 2.4 Invalid importance and invalid evidence parameters return 400 with no model calls
- [ ] 2.5 Embedding error returns 502 without calling the chat model

## 3. Integration tests (Testcontainers pgvector, fake models)

- [ ] 3.1 Supported requirement is `met` citing its chunk, unsupported is `missing` without citations, in request order
- [ ] 3.2 `kind` `project` cites only project chunks
- [ ] 3.3 Three requirements cause three judge calls; an empty store gives `missing` with no judge calls
- [ ] 3.4 Chat model error and invalid judge output return 502
- [ ] 3.5 A judge call records a chat span with the model name and token counts

## 4. Eval and docs

- [ ] 4.1 Add the fit-judgment eval (golden CV, supported and unsupported requirements, accuracy ≥ 0.8, skipped without credentials); run it with Ollama and report the result
- [ ] 4.2 Update README (endpoint table, how scoring works, roadmap status); verify `openspec validate --all --strict` and `dotnet test JobFit.slnx` pass

## Workflow follow-up

- Archive the change after its PR is merged.
