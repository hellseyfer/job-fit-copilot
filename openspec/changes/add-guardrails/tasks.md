# Tasks

## 1. Contact-data masking

- [ ] 1.1 Add `ContactDataMasker` (email, phone, URL) and verify with API tests, including years, ranges and percentages left unchanged
- [ ] 1.2 Mask knowledge content before chunking, hashing and embedding; verify with an integration test that stored chunks and embedding inputs are masked
- [ ] 1.3 Document masking and re-ingestion in the README knowledge section

## 2. Prompt-injection screen

- [ ] 2.1 Add normalization and `InjectionPatterns` with the shared delimiter list; verify with API tests for overrides, role markers, template tokens, delimiters, obfuscation and benign texts
- [ ] 2.2 Add `InjectionClassifier` (windows, rationale-first structured output, bounded concurrency, fail closed) and `GuardrailOptions`; verify with API tests for injection, benign, invalid verdict and model failure
- [ ] 2.3 Add `InputGuard` and `PromptInjectionException` with the 422 problem; call it from extraction, analyses, knowledge, fit scoring and approval; verify with API tests per entry point that no model call is made and the text is not echoed
- [ ] 2.4 Integration tests: a blocked CV is not stored; a blocked feedback leaves the run awaiting approval and decidable

## 3. Telemetry

- [ ] 3.1 Add the `JobFit.Guardrails` meter and span event, register it in ServiceDefaults; verify with a `MeterListener` test
- [ ] 3.2 Drop `UseLogging` from the chat and embedding pipelines; verify with an API test at `Trace` level that no log record or span contains the posting or evidence

## 4. Eval

- [ ] 4.1 Add `golden/guardrails.json` and `GuardrailEvals` reporting recall and false-positive rate overall and per layer; run it with Ollama and record the result

## 5. Integration checks

- [ ] 5.1 README (guardrails section, status, roadmap) and `.http` examples
- [ ] 5.2 `openspec validate --all --strict` and `dotnet test JobFit.slnx`
