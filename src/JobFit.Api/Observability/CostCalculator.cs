namespace JobFit.Api.Observability;

public static class CostCalculator
{
    private static readonly Dictionary<string, ModelPrice> DefaultPricing = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gpt-4o-mini"] = new ModelPrice { InputPerMillionUsd = 0.15, OutputPerMillionUsd = 0.60 },
        ["gpt-4.1-mini"] = new ModelPrice { InputPerMillionUsd = 0.40, OutputPerMillionUsd = 1.60 },
        ["text-embedding-3-small"] = new ModelPrice { InputPerMillionUsd = 0.02, OutputPerMillionUsd = 0 },
    };

    public static double Calculate(
        string? modelId,
        long inputTokens,
        long outputTokens,
        ObservabilityOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return 0;
        }

        ModelPrice? price = null;
        if (options?.ModelPricing is { Count: > 0 } configured)
        {
            foreach (var (key, value) in configured)
            {
                if (string.Equals(key, modelId, StringComparison.OrdinalIgnoreCase))
                {
                    price = value;
                    break;
                }
            }
        }

        price ??= DefaultPricing.GetValueOrDefault(modelId);
        if (price is null)
        {
            return 0;
        }

        var cost = (inputTokens * price.InputPerMillionUsd + outputTokens * price.OutputPerMillionUsd) / 1_000_000d;
        return Math.Round(cost, 8, MidpointRounding.AwayFromZero);
    }
}
