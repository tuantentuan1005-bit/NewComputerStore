using Microsoft.AspNetCore.SignalR;
using NewComputerStore.Hubs;
using NewComputerStore.Models;
using System.Threading.Tasks;

namespace NewComputerStore.Services
{
    public interface INotificationService
    {
        Task NotifyNewMessageAsync(ChatMessage message);
        Task NotifyConversationUpdatedAsync(ChatConversation conversation);
    }

    public class NotificationService : INotificationService
    {
        private readonly IHubContext<ChatHub> _hubContext;

        public NotificationService(IHubContext<ChatHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task NotifyNewMessageAsync(ChatMessage message)
        {
            // Send to everyone in the conversation group
            await _hubContext.Clients.Group($"Conversation_{message.ConversationId}").SendAsync("ReceiveMessage", new {
                id = message.Id,
                conversationId = message.ConversationId,
                senderId = message.SenderId,
                senderName = message.Sender?.FullName ?? "Unknown",
                content = message.Content,
                type = message.MessageType.ToString(),
                metadata = message.Metadata,
                sentAt = message.SentAt.ToString("HH:mm"),
                isStaff = message.SenderId != message.Conversation?.CustomerId
            });

            // Also notify Admin/Staff globally to update their conversation list if they are not in the group
            await _hubContext.Clients.All.SendAsync("GlobalNewMessage", message.ConversationId);
        }

        public async Task NotifyConversationUpdatedAsync(ChatConversation conversation)
        {
            await _hubContext.Clients.All.SendAsync("ConversationStatusChanged", conversation.Id, conversation.Status.ToString());
        }
    }
}
