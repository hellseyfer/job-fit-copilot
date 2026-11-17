using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace JobFit.Api.Guardrails;

// Screens untrusted text before any model call; throws PromptInjectionException when the text is blocked.
public sealed class InputGuard(InjectionClassifier classifier, IOptions<GuardrailOptions> options)
{
    public const string BlockedCounter = "jobfit.guardrails.blocked";
    public const string ProblemType = "https://job-fit-copilot/problems/prompt-injection";

    private static readonly Meter Meter = new(Microsoft.Extensions.Hosting.Extensions.GuardrailsMeter);
    private static readonly Counter<long> Blocked = Meter.CreateCounter<long>(BlockedCounter);

    public async Task ScreenAsync(string field, string? text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (InjectionPatterns.Matches(text))
        {
            throw Block(field, GuardrailLayer.Pattern);
        }

        if (options.Value.Classifier && await classifier.IsInjectionAsync(field, text, cancellationToken))
        {
            throw Block(field, GuardrailLayer.Classifier);
        }
    }

    public static ProblemHttpResult Problem(PromptInjectionException exception) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status422UnprocessableEntity,
            type: ProblemType,
            title: "The input looks like a prompt injection.",
            extensions: new Dictionary<string, object?>
            {
                ["field"] = exception.Field,
                ["layer"] = Name(exception.Layer),
            });

    public static ProblemHttpResult Problem(GuardrailFailedException exception) =>
        TypedResults.Problem(statusCode: StatusCodes.Status502BadGateway, title: exception.Message);

    private static PromptInjectionException Block(string field, GuardrailLayer layer)
    {
        var tags = new TagList { { "field", field }, { "layer", Name(layer) } };
        Blocked.Add(1, tags);
        Activity.Current?.AddEvent(new ActivityEvent("guardrails.blocked", tags: new ActivityTagsCollection(tags)));
        return new PromptInjectionException(field, layer);
    }

    private static string Name(GuardrailLayer layer) => layer == GuardrailLayer.Pattern ? "pattern" : "classifier";
}
