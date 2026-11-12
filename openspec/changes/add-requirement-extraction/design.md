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
- **Provider by configuration: OpenAI by default, Ollama as the local, free alternative.**
  `Llm:Provider` (`openai` | `ollama`) and `Llm:Model` are read by the AppHost, which declares a
  `chat` model resource for the chosen provider and passes the provider name to the API.
  - `openai`: `Aspire.Hosting.OpenAI` with the API key as a secret parameter (`openai-api-key`,
    user secrets locally); default model `gpt-4o-mini` (cheap, fast, native structured outputs).
  - `ollama`: CommunityToolkit Ollama hosting runs the Ollama container and pulls the model
    (default `qwen2.5:3b`); the API waits until the model is ready. No key, no cost.
  - The API registers the matching Aspire client integration (`Aspire.OpenAI` or
    `CommunityToolkit.Aspire.OllamaSharp`) as `IChatClient`. The extractor only sees `IChatClient`.
  Alternatives: a single OpenAI-compatible client pointed at Ollama's `/v1` endpoint (fewer
  packages, but hides that the abstraction is what makes providers swappable); Azure OpenAI (adds
  a provider without adding anything to show at this stage).
- **One middleware pipeline for every provider.** The integrations' own tracing is disabled and the
  API wraps the provider client with the same pipeline (`UseOpenTelemetry()` with the GenAI semantic
  conventions: model, token usage; then `UseLogging()`), so telemetry is identical whatever the
  provider and the pipeline can be tested with a fake inner client. Message content is not recorded.
  Alternative: custom spans; the built-in middleware already follows the OTel GenAI conventions.
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
  - An opt-in eval test (`JobFit.Evals`, skipped unless `OPENAI_API_KEY` or `OLLAMA_ENDPOINT` is
    set) runs a small golden set of postings against a real model and checks must-have recall. It becomes the CI quality
    gate in stage 8.

## Risks / Trade-offs

- [Model paraphrases instead of quoting] → grounding drops those requirements; the prompt insists on
  verbatim excerpts and the eval measures recall to detect over-filtering.
- [Structured output support differs across providers (e.g. small Ollama models)] → the 502 path
  covers invalid output; the provider choice is configuration only.
- [`Aspire.OpenAI` client integration is still preview] → it only parses the connection string and
  registers the client; falling back to `Microsoft.Extensions.AI.OpenAI` is a small change.
- [Small local models are slower and less accurate] → Ollama is for keyless local runs; quality is
  measured with the eval against the default provider.
- [Integration tests boot the AppHost with the default provider] → they pass a dummy API key; no
  LLM call is made by the health tests.
- [Standard HTTP resilience cancels model calls after 10 s per attempt] → the API raises the attempt
  and total timeouts for its HTTP clients; a local CPU model takes 30–60 s per posting.
- [API key leakage] → secret Aspire parameter / user secrets only; nothing in appsettings.
