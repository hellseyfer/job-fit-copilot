# Design: Refine Demo UI and Cover Letter Writer Empty-Draft Retry

## Demo UI
1. **Clear previous analysis on Analyze Fit**:
   - In `App.analyze()`, immediately reset `analysisResult.set(null)`, `feedback.set('')`, and `copied.set(false)` before sending `POST /api/analyses`.
   - In `app.html`, when `analysisResult()` is `null` and `analyzing()` is `true`, render a loading state in the results column instead of the idle "Ready for Analysis" message.
2. **Copy Cover Letter without references**:
   - Add `plainCoverLetter(letter: CoverLetter): string` in `App` that extracts `letter.sentences`, strips any stray inline numeric citation brackets (`/\s*\[\d+(?:\s*,\s*\d+)*\]/g`), trims each sentence, and joins them with a single space (`' '`), excluding the UI's `[documentId#chunkIndex]` badges.
   - Add `copyCoverLetter()` calling `navigator.clipboard.writeText(...)` and toggling a `copied` signal to show transient confirmation (`Copied!`).
   - Place the button in the Cover Letter card header alongside the attempts counter.

## Cover Letter Writer Retry
- Small local models (`qwen2.5:3b`) under JSON-schema grammar constraints at `Temperature = 0` can occasionally emit `{"sentences": []}` on longer prompts.
- Clarify in `CoverLetterWriter.SystemPrompt` that the JSON response must contain a non-empty `"sentences"` array.
- In `CoverLetterWriter.DraftAsync`, after deserializing `WrittenLetter`, check if it contains any non-blank sentence. If not, perform one retry appending a corrective user turn and setting `Temperature = 0.2f` so the model does not deterministically repeat the empty array.
- Pass the final `WrittenLetter` to `CoverLetterGrounding.Sentences(...)`, which still throws `CoverLetterFailedException("The cover letter draft has no sentences.")` if the retry also yields no sentences.
