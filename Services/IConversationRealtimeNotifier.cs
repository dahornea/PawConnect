using System.Collections.Concurrent;

namespace PawConnect.Services;

public interface IConversationRealtimeNotifier
{
    IDisposable Subscribe(int conversationId, Func<MessageDto, Task> handler);

    IDisposable SubscribeReaction(int conversationId, Func<MessageReactionUpdateDto, Task> handler);

    IDisposable SubscribeTyping(int conversationId, Func<MessageTypingIndicatorDto, Task> handler);

    Task PublishAsync(MessageDto message);

    Task PublishReactionAsync(MessageReactionUpdateDto update);

    Task PublishTypingAsync(MessageTypingIndicatorDto typingIndicator);
}

public sealed class ConversationRealtimeNotifier : IConversationRealtimeNotifier
{
    private readonly ConcurrentDictionary<Guid, Subscription> _subscriptions = new();
    private readonly ConcurrentDictionary<Guid, ReactionSubscription> _reactionSubscriptions = new();
    private readonly ConcurrentDictionary<Guid, TypingSubscription> _typingSubscriptions = new();

    public IDisposable Subscribe(int conversationId, Func<MessageDto, Task> handler)
    {
        var subscription = new Subscription(
            Guid.NewGuid(),
            conversationId,
            handler,
            () => { });

        subscription = subscription with
        {
            DisposeAction = () => _subscriptions.TryRemove(subscription.Id, out _)
        };

        _subscriptions[subscription.Id] = subscription;
        return subscription;
    }

    public async Task PublishAsync(MessageDto message)
    {
        var handlers = _subscriptions.Values
            .Where(subscription => subscription.ConversationId == message.ConversationId)
            .Select(subscription => subscription.Handler)
            .ToList();

        foreach (var handler in handlers)
        {
            await handler(message);
        }
    }

    public IDisposable SubscribeReaction(int conversationId, Func<MessageReactionUpdateDto, Task> handler)
    {
        var subscription = new ReactionSubscription(
            Guid.NewGuid(),
            conversationId,
            handler,
            () => { });

        subscription = subscription with
        {
            DisposeAction = () => _reactionSubscriptions.TryRemove(subscription.Id, out _)
        };

        _reactionSubscriptions[subscription.Id] = subscription;
        return subscription;
    }

    public async Task PublishReactionAsync(MessageReactionUpdateDto update)
    {
        var handlers = _reactionSubscriptions.Values
            .Where(subscription => subscription.ConversationId == update.ConversationId)
            .Select(subscription => subscription.Handler)
            .ToList();

        foreach (var handler in handlers)
        {
            await handler(update);
        }
    }

    public IDisposable SubscribeTyping(int conversationId, Func<MessageTypingIndicatorDto, Task> handler)
    {
        var subscription = new TypingSubscription(
            Guid.NewGuid(),
            conversationId,
            handler,
            () => { });

        subscription = subscription with
        {
            DisposeAction = () => _typingSubscriptions.TryRemove(subscription.Id, out _)
        };

        _typingSubscriptions[subscription.Id] = subscription;
        return subscription;
    }

    public async Task PublishTypingAsync(MessageTypingIndicatorDto typingIndicator)
    {
        var handlers = _typingSubscriptions.Values
            .Where(subscription => subscription.ConversationId == typingIndicator.ConversationId)
            .Select(subscription => subscription.Handler)
            .ToList();

        foreach (var handler in handlers)
        {
            await handler(typingIndicator);
        }
    }

    private sealed record Subscription(
        Guid Id,
        int ConversationId,
        Func<MessageDto, Task> Handler,
        Action DisposeAction) : IDisposable
    {
        public void Dispose()
        {
            DisposeAction();
        }
    }

    private sealed record ReactionSubscription(
        Guid Id,
        int ConversationId,
        Func<MessageReactionUpdateDto, Task> Handler,
        Action DisposeAction) : IDisposable
    {
        public void Dispose()
        {
            DisposeAction();
        }
    }

    private sealed record TypingSubscription(
        Guid Id,
        int ConversationId,
        Func<MessageTypingIndicatorDto, Task> Handler,
        Action DisposeAction) : IDisposable
    {
        public void Dispose()
        {
            DisposeAction();
        }
    }
}
