using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PawConnect.Data;
using PawConnect.Services;

namespace PawConnect.Hubs;

[Authorize]
public class AdoptionChatHub(
    IConversationService conversationService,
    IMessageService messageService,
    IConversationRealtimeNotifier realtimeNotifier) : Hub
{
    public static string GetConversationGroupName(int conversationId)
    {
        return $"conversation-{conversationId}";
    }

    public async Task JoinConversation(int conversationId)
    {
        var userId = GetCurrentUserId();
        if (!await conversationService.CanAccessConversationAsync(conversationId, userId, Context.ConnectionAborted))
        {
            throw new HubException("You cannot access this conversation.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GetConversationGroupName(conversationId), Context.ConnectionAborted);
    }

    public Task LeaveConversation(int conversationId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, GetConversationGroupName(conversationId), Context.ConnectionAborted);
    }

    public Task<MessageDto> SendMessage(int conversationId, string body)
    {
        return messageService.SendMessageAsync(conversationId, GetCurrentUserId(), body, Context.ConnectionAborted);
    }

    public Task<MessageReactionUpdateDto> ToggleReaction(int messageId, string reactionType)
    {
        return messageService.ToggleReactionAsync(messageId, reactionType, GetCurrentUserId(), Context.ConnectionAborted);
    }

    public Task<MessageDto> EditMessage(int messageId, string newBody)
    {
        return messageService.EditMessageAsync(messageId, newBody, GetCurrentUserId(), Context.ConnectionAborted);
    }

    public async Task StartTyping(int conversationId)
    {
        await PublishTypingStateAsync(conversationId, isTyping: true);
    }

    public async Task StopTyping(int conversationId)
    {
        await PublishTypingStateAsync(conversationId, isTyping: false);
    }

    private async Task PublishTypingStateAsync(int conversationId, bool isTyping)
    {
        var userId = GetCurrentUserId();
        if (!await conversationService.CanAccessConversationAsync(conversationId, userId, Context.ConnectionAborted))
        {
            throw new HubException("You cannot access this conversation.");
        }

        var typingIndicator = new MessageTypingIndicatorDto(
            conversationId,
            userId,
            GetCurrentUserRoleLabel(),
            isTyping);

        await realtimeNotifier.PublishTypingAsync(typingIndicator);
        await Clients
            .OthersInGroup(GetConversationGroupName(conversationId))
            .SendAsync("ReceiveTyping", typingIndicator, Context.ConnectionAborted);
    }

    private string GetCurrentUserId()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new HubException("Current user could not be found.");
        }

        return userId;
    }

    private string GetCurrentUserRoleLabel()
    {
        return Context.User?.IsInRole(IdentitySeedData.ShelterRole) == true
            ? "Shelter"
            : "Adopter";
    }
}
