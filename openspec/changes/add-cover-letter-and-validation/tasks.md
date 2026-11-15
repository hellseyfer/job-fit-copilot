## 1. Cover letter

- [ ] 1.1 Add `CoverLetters/` contracts (`LetterDraft`, `CoverLetter`, sentence and verdict outputs) and `CoverLetterFailedException`
- [ ] 1.2 Add evidence numbering and citation grounding (`CoverLetterGrounding`)
- [ ] 1.3 Add `CoverLetterWriter` (first draft and rewrite with feedback) with structured output and delimiters
- [ ] 1.4 Add `CoverLetterVerifier`: uncited claims unsupported without a call, one call for the rest, missing verdicts unsupported
- [ ] 1.5 Replace `strong-fit` in `JobAnalysisWorkflow` with write → verify → (rewrite | finalize), add `coverLetter` to the response and map failures to 502

## 2. API tests

- [ ] 2.1 Evidence numbering: only met/partial citations, distinct chunks, numbered from 1
- [ ] 2.2 Citation resolution: out-of-range and duplicate numbers dropped
- [ ] 2.3 Uncited claim is unsupported and not sent to the verifier; missing verdict is unsupported
- [ ] 2.4 Loop decision: rewrite due only while unsupported and fewer than 3 drafts; finalize removes unsupported sentences

## 3. Integration tests (pgvector)

- [ ] 3.1 Strong fit accepted first time: path, letter, attempts 1, no study plan, planner not called
- [ ] 3.2 Rewrite fixes the letter: path with two write/verify rounds, feedback sent to the writer, attempts 2
- [ ] 3.3 Still unsupported after 3 drafts: sentence removed and reported, writer called 3 times
- [ ] 3.4 Weak fit: no cover letter and the cover-letter models are not called
- [ ] 3.5 Writer and verifier failures (error and invalid output) return 502
- [ ] 3.6 Existing analysis tests updated for the new strong-fit path

## 4. Eval and docs

- [ ] 4.1 Verifier eval on labeled sentences (accuracy ≥ 0.8)
- [ ] 4.2 Letter eval on the golden CV (≥ 3 sentences, no facts absent from the CV)
- [ ] 4.3 README (graph, `coverLetter`, roadmap status) and `.http` example
- [ ] 4.4 `openspec validate --all --strict` and `dotnet test JobFit.slnx`
