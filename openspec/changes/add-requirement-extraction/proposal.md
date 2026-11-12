# Proposal

## Why

Every later stage (evidence retrieval, fit scoring, cover letter) works per requirement, so the first
AI step is turning a free-text job posting into a structured, trustworthy list of requirements. It is
also the smallest useful LLM feature: one call, structured output, grounding and telemetry, without
RAG or a workflow yet.

## What Changes

- Add `POST /api/requirements/extract`: receives job posting text and returns its title, seniority
  and a list of requirements, each with category, importance (must-have / nice-to-have) and a
  verbatim excerpt of the posting that supports it.
- Use an LLM with structured output (JSON schema) behind the `IChatClient` abstraction; the provider
  is selected by configuration.
- Ground the output: requirements whose excerpt does not appear in the posting are discarded.
- Validate input (empty or oversized postings are rejected) and map LLM failures to a clear error.
- Trace each LLM call with OpenTelemetry (model, latency, token usage) in the Aspire dashboard.
- Add a small golden set of job postings and an opt-in eval test that runs against a real model.

Out of scope: CV parsing, embeddings and retrieval, fit scoring, the agent workflow graph, prompt
injection and PII guardrails (stage 7), the evaluation quality gate in CI (stage 8), cost tracking
and Langfuse export (stage 9), persistence of extraction results, authentication.

## Capabilities

### New Capabilities
- `requirement-extraction`: turning a job posting into structured, grounded requirements.

### Modified Capabilities
<!-- none -->

## Impact

- New endpoint and feature folder in `JobFit.Api`.
- AppHost declares the LLM as a resource; the API key is an Aspire secret parameter (user secrets
  locally, never committed).
- Dependencies: `Microsoft.Extensions.AI`, the provider adapter (OpenAI by default) and the matching
  Aspire integrations.
- Unit and API tests use a fake `IChatClient`, so CI needs no LLM credentials.
