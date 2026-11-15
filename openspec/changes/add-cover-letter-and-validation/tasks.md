## 1. Cover letter

- [x] 1.1 Add `CoverLetters/` contracts (`LetterDraft`, `CoverLetter`, sentence and verdict outputs) and `CoverLetterFailedException`
- [x] 1.2 Add evidence numbering and citation grounding (`CoverLetterGrounding`)
- [x] 1.3 Add `CoverLetterWriter` (first draft and rewrite with feedback) with structured output and delimiters
- [x] 1.4 Add `CoverLetterVerifier`: uncited claims unsupported without a call, one call for the rest, missing verdicts unsupported
- [x] 1.5 Replace `strong-fit` in `JobAnalysisWorkflow` with write → verify → (rewrite | finalize), add `coverLetter` to the response and map failures to 502

## 2. API tests

- [x] 2.1 Evidence numbering: only met/partial citations, distinct chunks, numbered from 1
- [x] 2.2 Citation resolution: out-of-range and duplicate numbers dropped
- [x] 2.3 Uncited claim is unsupported and not sent to the verifier; missing verdict is unsupported
- [x] 2.4 Loop decision: rewrite due only while unsupported and fewer than 3 drafts; finalize removes unsupported sentences

## 3. Integration tests (pgvector)

- [x] 3.1 Strong fit accepted first time: path, letter, attempts 1, no study plan, planner not called
- [x] 3.2 Rewrite fixes the letter: path with two write/verify rounds, feedback sent to the writer, attempts 2
- [x] 3.3 Still unsupported after 3 drafts: sentence removed and reported, writer called 3 times
- [x] 3.4 Weak fit: no cover letter and the cover-letter models are not called
- [x] 3.5 Writer and verifier failures (error and invalid output) return 502
- [x] 3.6 Existing analysis tests updated for the new strong-fit path

## 4. Eval and docs

- [x] 4.1 Verifier eval on labeled sentences (accuracy ≥ 0.8)
- [x] 4.2 Letter eval on the golden CV (≥ 3 sentences, no facts absent from the CV)
- [x] 4.3 README (graph, `coverLetter`, roadmap status) and `.http` example
- [x] 4.4 `openspec validate --all --strict` and `dotnet test JobFit.slnx`
