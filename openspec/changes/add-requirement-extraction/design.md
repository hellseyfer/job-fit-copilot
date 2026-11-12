# Design

## Context

Stage 0 provides the API, Postgres and OpenTelemetry through Aspire. This is the first change that
calls an LLM. AGENTS.md requires LLM access through `Microsoft.Extensions.AI` (`IChatClient`) and every
spec scenario to be covered by an automated test that runs in CI without secrets.

## Goals / Non-Goals

**Goals:**
- One provider-agnostic extraction component that later becomes a node of the workflow graph.
- Deterministic tests for every scenario, without network or credentials.
- LLM calls visible in the Aspire dashboard (spans with model and tokens).

**Non-Goals:**
- Prompt-injection defenses beyond keeping the posting delimited as data in the prompt (stage 7).
- Caching, retries tuned for cost, or streaming responses.

## Decisions

- **`IChatClient` + structured output.** The extractor calls `GetResponseAsync<T>` with a C# record,
  so the JSON schema is derived from the type and the provider enforces it where supported
  (OpenAI structured outputs). Alternative: free-form JSON in the prompt and manual parsing; less
  reliable and more code. A vendor SDK directly is ruled out by AGENTS.md.
- **Provider by configuration, OpenAI by default.** The AppHost declares the model resource and the
  API key as a secret parameter; the API registers the `IChatClient` through the Aspire client
  integration. `gpt-4o-mini`-class model: cheap, fast and good at structured output. Ollama stays
  possible as a keyless alternative by swapping the registration, without touching the extractor.
- **Middleware pipeline on the client.** `IChatClient` is built with `UseOpenTelemetry()` (GenAI
  semantic conventions: model, token usage) and `UseLogging()`. Alternative: custom spans; the
  built-in middleware already follows the OTel GenAI conventions.
- **Grounding check in code, not in the prompt.** The model is asked for a verbatim excerpt per
  requirement; a deterministic filter normalizes case and whitespace and drops requirements whose
  excerpt is not in the posting. This is the first anti-hallucination layer and is unit-testable.
  Alternative: LLM-as-judge validation; slower, costlier and itself non-deterministic.
- **Ids assigned by code.** `req-1`, `req-2`… in output order after filtering, so they are unique and
  stable regardless of the model.
- **Temperature 0** to reduce variance between runs.
- **Feature folder `Requirements/` in `JobFit.Api`**: endpoint, request/response contracts, extractor
  and grounding filter. A separate class library is deferred until the workflow stage needs to share it.
- **Error mapping.** Input validation runs before the model (400). Provider exceptions and
  deserialization failures map to 502 problem details; details are logged, not returned.
- **Tests.**
  - `JobFit.Api.Tests` replaces `IChatClient` with a fake that returns canned responses or throws,
    covering every scenario through HTTP, and asserts whether the model was called.
  - The trace scenario uses an in-memory `ActivityListener` with the fake client reporting usage.
  - An opt-in eval test (`JobFit.Evals`, skipped when no API key is configured) runs a small golden
    set of postings against the real model and checks must-have recall. It becomes the CI quality
    gate in stage 8.

## Risks / Trade-offs

- [Model paraphrases instead of quoting] → grounding drops those requirements; the prompt insists on
  verbatim excerpts and the eval measures recall to detect over-filtering.
- [Structured output support differs across providers (e.g. small Ollama models)] → the 502 path
  covers invalid output; the provider choice is configuration only.
- [Aspire OpenAI client integration is still preview] → it only wires configuration and telemetry;
  falling back to `Microsoft.Extensions.AI.OpenAI` registration is a small change.
- [API key leakage] → secret Aspire parameter / user secrets only; nothing in appsettings.
