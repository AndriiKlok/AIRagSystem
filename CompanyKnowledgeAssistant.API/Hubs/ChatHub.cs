using Microsoft.AspNetCore.SignalR;

namespace CompanyKnowledgeAssistant.API.Hubs;

public class ChatHub : Hub
{
    public async Task JoinChat(int chatId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"chat-{chatId}");
    }

    public async Task LeaveChat(int chatId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat-{chatId}");
    }

    public async Task SendMessage(int chatId, object message)
    {
        await Clients.Group($"chat-{chatId}").SendAsync("ReceiveMessage", message);
    }
}