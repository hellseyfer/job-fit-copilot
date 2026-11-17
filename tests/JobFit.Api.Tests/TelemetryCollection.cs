namespace JobFit.Api.Tests;

// Tests that listen to process-wide activities or meters run alone, so other tests' model calls do not reach them.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TelemetryCollection
{
    public const string Name = "Telemetry";
}
