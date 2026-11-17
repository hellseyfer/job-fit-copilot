namespace JobFit.Api.Observability;

public sealed class ModelPrice
{
    public double InputPerMillionUsd { get; set; }

    public double OutputPerMillionUsd { get; set; }
}

public sealed class LangfuseOptions
{
    public const string DefaultEndpoint = "https://cloud.langfuse.com/api/public/otel";

    public string Endpoint { get; set; } = DefaultEndpoint;

    public string? PublicKey { get; set; }

    public string? SecretKey { get; set; }
}

public sealed class ObservabilityOptions
{
    public Dictionary<string, ModelPrice> ModelPricing { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public LangfuseOptions Langfuse { get; set; } = new();
}
