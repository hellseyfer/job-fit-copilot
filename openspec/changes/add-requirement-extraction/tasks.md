# Tasks

## 1. LLM wiring

- [ ] 1.1 Declare the chat model resource and a secret API key parameter in the AppHost, referenced by the API; verify `dotnet run --project src/JobFit.AppHost` prompts for or reads the key from user secrets
- [ ] 1.2 Register `IChatClient` in the API with OpenTelemetry and logging middleware; verify the API starts and `/health` still returns 200

## 2. Extraction

- [ ] 2.1 Add request/response contracts and the structured-output record for requirements (title, seniority, category, importance, excerpt); verify the solution builds
- [ ] 2.2 Implement the grounding filter (case and whitespace insensitive) and id assignment; verify unit tests for ungrounded excerpts, case/whitespace differences and unique ids pass
- [ ] 2.3 Implement the extractor prompt (posting delimited as data, verbatim excerpts, importance rules, temperature 0)
- [ ] 2.4 Map `POST /api/requirements/extract` with input validation and 502 error mapping; add an example to `JobFit.Api.http`

## 3. API tests (fake `IChatClient`)

- [ ] 3.1 Posting with several requirements returns 200 with categories and excerpts
- [ ] 3.2 Must-have and nice-to-have importance are returned as produced for required and optional items
- [ ] 3.3 Ungrounded requirement is removed while grounded ones are returned
- [ ] 3.4 Empty and oversized postings return 400 and the model is not called
- [ ] 3.5 Provider error and invalid model output return 502
- [ ] 3.6 Text without requirements returns 200 with an empty list
- [ ] 3.7 Model call span records model name and token usage

## 4. Eval and docs

- [ ] 4.1 Add `JobFit.Evals` with a golden set of 3 job postings and expected must-have requirements; the test is skipped without an API key and reports must-have recall when run
- [ ] 4.2 Update README (endpoint, how to set the API key, how to run the eval) and AGENTS.md if new conventions appear; verify `openspec validate --all --strict` and `dotnet test JobFit.slnx` pass

## Workflow follow-up

- Archive the change after the PR is merged.
