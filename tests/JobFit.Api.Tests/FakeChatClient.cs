using Microsoft.Extensions.AI;

namespace JobFit.Api.Tests;

public sealed class FakeChatClient : IChatClient
{
    public const string ModelId = "fake-model";

    private readonly Func<IEnumerable<ChatMessage>, ChatResponse> _respond;
    private int _callCount;

    private FakeChatClient(Func<IEnumerable<ChatMessage>, ChatResponse> respond) => _respond = respond;

    public int CallCount => _callCount;

    public static FakeChatClient Returning(string json) => Responding(_ => json);

    // Answers each request from its messages; must be thread-safe because calls may run concurrently.
    public static FakeChatClient Responding(Func<IEnumerable<ChatMessage>, string> json) => new(messages =>
        new ChatResponse(new ChatMessage(ChatRole.Assistant, json(messages)))
        {
            ModelId = ModelId,
            Usage = new UsageDetails { InputTokenCount = 120, OutputTokenCount = 45, TotalTokenCount = 165 },
        });

    public static FakeChatClient Throwing(Exception exception) => new(_ => throw exception);

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _callCount);
        return Task.FromResult(_respond(messages));
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
