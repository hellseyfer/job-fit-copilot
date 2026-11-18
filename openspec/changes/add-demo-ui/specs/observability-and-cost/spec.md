# Spec Delta

## MODIFIED Requirements

### Requirement: Cost is calculated from configurable model pricing
The system SHALL compute `costUsd` from `Observability:ModelPricing` (per-million input and output USD rates),
matching exact model IDs or dated snapshot IDs by longest prefix, falling back to built-in defaults for known
OpenAI models and `0` for unknown or local models.

#### Scenario: Known model uses configured or default pricing
- **WHEN** a model call reports input and output tokens for `gpt-4o-mini`, a dated snapshot such as `gpt-4o-mini-2024-07-18`, or an overridden entry in `Observability:ModelPricing`
- **THEN** `costUsd` equals `(inputTokens * inputRate + outputTokens * outputRate) / 1_000_000`

#### Scenario: Unknown or local model has zero cost
- **WHEN** a model call uses a model with no configured pricing (such as `qwen2.5:3b`)
- **THEN** `costUsd` is `0`
