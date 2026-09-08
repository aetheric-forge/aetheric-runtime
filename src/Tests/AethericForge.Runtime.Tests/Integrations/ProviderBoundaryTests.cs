using AethericForge.Runtime.Abstractions.Interfaces.Conversation.Consumers;
using AethericForge.Runtime.Abstractions.Interfaces.Conversation.Primitives;
using AethericForge.Runtime.Abstractions.Interfaces.TableTop.Consumers;
using AethericForge.Runtime.Abstractions.Interfaces.TableTop.Primitives;
using AethericForge.Runtime.Models.Conversation;
using AethericForge.Runtime.Models.TableTop;
using Xunit;

namespace AethericForge.Runtime.Tests.Integrations;

public sealed class ProviderBoundaryTests
{
    [Fact]
    public async Task Conversation_preserves_thread_operation_and_text_and_returns_owned_subscription()
    {
        var provider = new TestConversationProvider();
        var reference = new ConversationReference("chat", "workspace", "channel");
        using var cancellation = new CancellationTokenSource();
        await provider.SendMessageAsync(reference, "  narrative\n", "operation-1", "thread-1", cancellation.Token);
        Assert.Equal((reference, "  narrative\n", "operation-1", "thread-1", cancellation.Token), provider.Sent);
        var subscription = await provider.SubscribeAsync(reference, new ConversationConsumer());
        await subscription.DisposeAsync();
        Assert.True(provider.Subscription.Disposed);
    }

    [Fact]
    public async Task Conversation_rejects_wrong_provider_and_cancelled_calls_before_dispatch()
    {
        var provider = new TestConversationProvider();
        var wrong = new ConversationReference("other", "workspace", "channel");
        Assert.Throws<ArgumentException>(() => provider.ReadMessagesAsync(wrong));
        await Assert.ThrowsAsync<ArgumentException>(() => provider.SendMessageAsync(wrong, "text", "operation"));
        await Assert.ThrowsAsync<ArgumentException>(() => provider.SubscribeAsync(wrong, new ConversationConsumer()));
        await Assert.ThrowsAsync<OperationCanceledException>(() => provider.SendMessageAsync(
            new ConversationReference("chat", "workspace", "channel"), "text", "operation", ct: new CancellationToken(true)));
        Assert.Null(provider.Sent);
    }

    [Fact]
    public async Task Tabletop_forwards_scene_operation_and_cancellation_without_vendor_payloads()
    {
        var provider = new TestTableTopProvider();
        var reference = new TableTopReference("table", "workspace", "room");
        using var cancellation = new CancellationTokenSource();
        await provider.SetActiveSceneAsync(reference, "scene", "operation", cancellation.Token);
        Assert.Equal((reference, "scene", "operation", cancellation.Token), provider.Scene);
        Assert.Null(await provider.GetStateAsync(reference));
        var subscription = await provider.SubscribeAsync(reference, new TableTopConsumer());
        await subscription.DisposeAsync();
        Assert.True(provider.Subscription.Disposed);
    }

    [Fact]
    public async Task Tabletop_rejects_cross_provider_commands_and_missing_operation_ids()
    {
        var provider = new TestTableTopProvider();
        await Assert.ThrowsAsync<ArgumentException>(() => provider.SetActiveSceneAsync(new TableTopReference("other", "workspace", "room"), "scene", "op"));
        await Assert.ThrowsAsync<ArgumentException>(() => provider.SetActiveSceneAsync(new TableTopReference("table", "workspace", "room"), "scene", ""));
        await Assert.ThrowsAsync<OperationCanceledException>(() => provider.GetStateAsync(new TableTopReference("table", "workspace", "room"), new CancellationToken(true)));
        Assert.Null(provider.Scene);
    }

    [Fact]
    public void References_keep_workspace_identity_and_opaque_ids()
    {
        Assert.NotEqual(new ConversationReference("chat", "one", "channel"), new ConversationReference("chat", "two", "channel"));
        Assert.Equal(" CaseSensitive ", new TableTopReference("table", "workspace", " CaseSensitive ").Id);
        Assert.Throws<ArgumentException>(() => new TableTopReference("table", "", "room"));
    }

    private sealed record Message(IConversationReference Conversation, string Text, string? ThreadId) : IConversationMessage
    {
        public string Id => "message";
        public string ParticipantId => "participant";
        public DateTimeOffset SentAt => DateTimeOffset.UnixEpoch;
        public DateTimeOffset? EditedAt => null;
        public Uri? SourceUrl => null;
    }
    private sealed class Lease : IAsyncDisposable
    {
        public bool Disposed { get; private set; }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
    private sealed class ConversationConsumer : IConversationEventConsumer
    {
        public Task ConsumeAsync(IConversationEvent conversationEvent, CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class TableTopConsumer : ITableTopEventConsumer
    {
        public Task ConsumeAsync(ITableTopEvent tableTopEvent, CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class TestConversationProvider() : ConversationProvider("chat")
    {
        public (IConversationReference, string, string, string?, CancellationToken)? Sent;
        public Lease Subscription { get; } = new();
        protected override Task<IConversationMessage> SendMessageCoreAsync(IConversationReference conversation, string text, string operationId, string? threadId, CancellationToken ct)
        {
            Sent = (conversation, text, operationId, threadId, ct);
            return Task.FromResult<IConversationMessage>(new Message(conversation, text, threadId));
        }
        protected override async IAsyncEnumerable<IConversationMessage> ReadMessagesCoreAsync(IConversationReference conversation,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            yield break;
        }
        protected override Task<IAsyncDisposable> SubscribeCoreAsync(IConversationReference conversation, IConversationEventConsumer consumer, CancellationToken ct) => Task.FromResult<IAsyncDisposable>(Subscription);
    }
    private sealed class TestTableTopProvider() : TableTopProvider("table")
    {
        public (ITableTopReference, string, string, CancellationToken)? Scene;
        public Lease Subscription { get; } = new();
        protected override Task SetActiveSceneCoreAsync(ITableTopReference tableTop, string sceneId, string operationId, CancellationToken ct)
        {
            Scene = (tableTop, sceneId, operationId, ct);
            return Task.CompletedTask;
        }
        protected override Task<ITableTopState?> GetStateCoreAsync(ITableTopReference tableTop, CancellationToken ct) => Task.FromResult<ITableTopState?>(null);
        protected override Task<IAsyncDisposable> SubscribeCoreAsync(ITableTopReference tableTop, ITableTopEventConsumer consumer, CancellationToken ct) => Task.FromResult<IAsyncDisposable>(Subscription);
    }
}
