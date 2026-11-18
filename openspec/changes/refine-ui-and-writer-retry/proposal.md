# Proposal: Refine Demo UI and Cover Letter Writer Empty-Draft Retry

## Why
1. In the Demo UI, clicking **Analyze Fit** while a previous analysis is displayed leaves the old result visible while the new workflow runs (and even if the new request fails), causing confusion.
2. Users cannot easily copy the generated cover letter without the inline `[documentId#chunkIndex]` evidence badges rendered in the UI.
3. In the end-to-end eval suite running against small local models (`qwen2.5:3b`), `CoverLetterWriter` occasionally receives an empty `sentences` array from JSON-schema-constrained decoding at temperature 0, causing `Strong_fit_golden_posting_produces_a_grounded_cover_letter` to fail with `CoverLetterFailedException: The cover letter draft has no sentences.`

## What Changes
- **Demo UI (`demo-ui`)**:
  - Clear the current `analysisResult` (and pending feedback / copy confirmation state) immediately when starting a new analysis via **Analyze Fit**, and display a loading indicator while the workflow runs.
  - Add a **Copy Cover Letter** button in the Cover Letter card that copies the plain cover letter text without evidence references (`[documentId#chunkIndex]` or inline numeric citations).
- **Cover Letter Writer (`cover-letter`)**:
  - Clarify in `CoverLetterWriter`'s system prompt that `"sentences"` must be a non-empty array.
  - When the writer model returns a draft with no non-blank sentences, retry once with a corrective user message and a slight temperature bump (`0.2`) before failing with `CoverLetterFailedException` (502).

## Impact
- `src/JobFit.Web/src/app/app.ts`, `app.html`, `app.css`, `app.spec.ts`
- `src/JobFit.Api/CoverLetters/CoverLetterWriter.cs`
- `tests/JobFit.Api.Tests/CoverLetterTests.cs`
