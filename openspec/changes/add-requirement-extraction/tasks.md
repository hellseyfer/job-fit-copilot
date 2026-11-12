# Tasks

## 1. LLM wiring

- [x] 1.1 Declare the `chat` model resource in the AppHost from `Llm:Provider` / `Llm:Model`: OpenAI (default, secret `openai-api-key` parameter) or Ollama (container + model pull); verify the AppHost starts with each provider
- [x] 1.2 Register `IChatClient` in the API with the matching Aspire client integration and a shared OpenTelemetry + logging pipeline; add the GenAI trace source to ServiceDefaults; verify the API starts and `/health` still returns 200
- [x] 1.3 Pass a dummy OpenAI key in the AppHost integration tests and verify they still pass

## 2. Extraction

- [x] 2.1 Add request/response contracts and the structured-output record for requirements (title, seniority, category, importance, excerpt); verify the solution builds
- [x] 2.2 Implement the grounding filter (case and whitespace insensitive) and id assignment; verify unit tests for ungrounded excerpts, case/whitespace differences and unique ids pass
- [x] 2.3 Implement the extractor prompt (posting delimited as data, verbatim excerpts, importance rules, temperature 0)
- [x] 2.4 Map `POST /api/requirements/extract` with input validation and 502 error mapping; add an example to `JobFit.Api.http`

## 3. API tests (fake `IChatClient`)

- [x] 3.1 Posting with several requirements returns 200 with categories and excerpts
- [x] 3.2 Must-have and nice-to-have importance are returned as produced for required and optional items
- [x] 3.3 Ungrounded requirement is removed while grounded ones are returned
- [x] 3.4 Empty and oversized postings return 400 and the model is not called
- [x] 3.5 Provider error and invalid model output return 502
- [x] 3.6 Text without requirements returns 200 with an empty list
- [x] 3.7 Model call span records model name and token usage

## 4. Eval and docs

- [x] 4.1 Add `JobFit.Evals` with a golden set of 3 job postings and expected must-have requirements; the test is skipped unless `OPENAI_API_KEY` or `OLLAMA_ENDPOINT` is set and reports must-have recall when run
- [x] 4.2 Update README (endpoint, choosing the provider, setting the OpenAI key, running the eval) and AGENTS.md if new conventions appear; verify `openspec validate --all --strict` and `dotnet test JobFit.slnx` pass

## Workflow follow-up

- Archive the change after the PR is merged.
