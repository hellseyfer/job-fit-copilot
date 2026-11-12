using Microsoft.Extensions.AI;

namespace JobFit.Api.Tests;

public sealed class FakeChatClient : IChatClient
{
    public const string ModelId = "fake-model";

    private readonly Func<ChatResponse> _respond;

    private FakeChatClient(Func<ChatResponse> respond) => _respond = respond;

    public int CallCount { get; private set; }

    public static FakeChatClient Returning(string json) => new(() =>
        new ChatResponse(new ChatMessage(ChatRole.Assistant, json))
        {
            ModelId = ModelId,
            Usage = new UsageDetails { InputTokenCount = 120, OutputTokenCount = 45, TotalTokenCount = 165 },
        });

    public static FakeChatClient Throwing(Exception exception) => new(() => throw exception);

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(_respond());
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) =>
        serviceType == typeof(ChatClientMetadata) ? new ChatClientMetadata("fake", null, ModelId) : null;

    public void Dispose()
    {
    }
}
