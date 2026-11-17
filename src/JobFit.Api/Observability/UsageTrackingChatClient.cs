using Microsoft.Extensions.AI;

namespace JobFit.Api.Observability;

public sealed class UsageTrackingChatClient(IChatClient innerClient, UsageTracker tracker)
    : DelegatingChatClient(innerClient)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var response = await base.GetResponseAsync(messages, options, cancellationToken);
        var model = response.ModelId ?? this.GetService<ChatClientMetadata>()?.DefaultModelId;
        var inputTokens = response.Usage?.InputTokenCount ?? 0;
        var outputTokens = response.Usage?.OutputTokenCount ?? 0;
        var totalTokens = response.Usage?.TotalTokenCount ?? inputTokens + outputTokens;
        tracker.RecordCall(model, inputTokens, outputTokens, totalTokens);
        return response;
    }
}
