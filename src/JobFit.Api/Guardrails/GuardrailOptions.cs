namespace JobFit.Api.Guardrails;

public sealed class GuardrailOptions
{
    public bool Classifier { get; set; } = true;
}

public enum GuardrailLayer { Pattern, Classifier }

public sealed class PromptInjectionException(string field, GuardrailLayer layer)
    : Exception($"The {field} looks like a prompt injection.")
{
    public string Field { get; } = field;

    public GuardrailLayer Layer { get; } = layer;
}

public sealed class GuardrailFailedException(string message, Exception? innerException = null)
    : Exception(message, innerException);
