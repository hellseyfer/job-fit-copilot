# Tasks

- [x] 1. Clear `analysisResult`, `feedback`, and `copied` state in `App.analyze()` and show a loading card while analyzing in `app.html`.
- [x] 2. Add a Copy Cover Letter button in `app.html` / `app.ts` / `app.css` that copies the cover letter text without evidence references.
- [x] 3. Add Angular unit tests in `src/JobFit.Web/src/app/app.spec.ts` covering clearing previous analysis on Analyze Fit and copying the cover letter without references.
- [x] 4. Update `CoverLetterWriter` prompt and add single retry on empty `sentences` draft in `src/JobFit.Api/CoverLetters/CoverLetterWriter.cs`.
- [x] 5. Add unit tests in `tests/JobFit.Api.Tests/CoverLetterTests.cs` covering empty-draft retry success and failure when retry is also empty.
- [x] 6. Run `openspec validate --all --strict`, `dotnet test tests/JobFit.Api.Tests`, and `npm test` in `src/JobFit.Web`.
