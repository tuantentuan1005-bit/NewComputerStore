using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NewComputerStore.Data;
using NewComputerStore.Models;
using NewComputerStore.Services;

namespace NewComputerStore.Hubs
{
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme + "," + JwtBearerDefaults.AuthenticationScheme)]
    public class ChatHub : Hub
    {
        // Tracks online users: UserId -> List of ConnectionIds
        private static readonly ConcurrentDictionary<string, HashSet<string>> OnlineUsers = new();

        private readonly IChatService _chatService;
        private readonly INotificationService _notificationService;
        private readonly AppDbContext _context;

        public ChatHub(IChatService chatService, INotificationService notificationService, AppDbContext context)
        {
            _chatService = chatService;
            _notificationService = notificationService;
            _context = context;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                OnlineUsers.AddOrUpdate(
                    userId,
                    _ => new HashSet<string> { Context.ConnectionId },
                    (_, hs) =>
                    {
                        lock (hs)
                        {
                            hs.Add(Context.ConnectionId);
                        }
                        return hs;
                    });

                if (int.TryParse(userId, out var parsedUserId))
                {
                    var activeConversationIds = await _context.ChatConversations
                        .AsNoTracking()
                        .Where(c => c.CustomerId == parsedUserId && c.Status != ConversationStatus.Closed)
                        .Select(c => c.Id)
                        .ToListAsync();

                    foreach (var convId in activeConversationIds)
                    {
                        await Groups.AddToGroupAsync(Context.ConnectionId, $"Conversation_{convId}");
                    }
                }

                await Clients.All.SendAsync("UserStatusChanged", userId, true);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
            {
                if (OnlineUsers.TryGetValue(userId, out var connections))
                {
                    bool becameOffline = false;
                    lock (connections)
                    {
                        connections.Remove(Context.ConnectionId);
                        if (connections.Count == 0)
                        {
                            OnlineUsers.TryRemove(userId, out _);
                            becameOffline = true;
                        }
                    }

                    if (becameOffline)
                    {
                        await Clients.All.SendAsync("UserStatusChanged", userId, false);
                    }
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        public async Task JoinConversation(int conversationId)
        {
            var userIdStr = Context.UserIdentifier ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
            {
                throw new HubException("Không xác định được người dùng.");
            }

            if (conversationId <= 0)
            {
                var conv = await _chatService.GetOrCreateConversationAsync(userId);
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Conversation_{conv.Id}");
                return;
            }

            var conversation = await _context.ChatConversations
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null)
            {
                throw new HubException("Cuộc hội thoại không tồn tại.");
            }

            if (conversation.CustomerId != userId && !IsStaffOrAdmin())
            {
                throw new HubException("Bạn không có quyền tham gia cuộc hội thoại này.");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"Conversation_{conversationId}");
        }

        public async Task LeaveConversation(int conversationId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Conversation_{conversationId}");
        }

        public async Task SendTyping(int conversationId, string userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                userName = Context.User?.FindFirstValue(ClaimTypes.Name) ?? "Khách hàng";
            }

            await Clients.OthersInGroup($"Conversation_{conversationId}").SendAsync("UserTyping", conversationId, userName);
        }

        public async Task SendMessage(int conversationId, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new HubException("Nội dung tin nhắn không được để trống.");
            }

            var userIdStr = Context.UserIdentifier ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
            {
                throw new HubException("Không xác định được người dùng.");
            }

            ChatConversation? conversation;
            if (conversationId <= 0)
            {
                conversation = await _chatService.GetOrCreateConversationAsync(userId);
            }
            else
            {
                conversation = await _context.ChatConversations.FindAsync(conversationId);
                if (conversation == null)
                {
                    throw new HubException("Cuộc hội thoại không tồn tại.");
                }

                if (conversation.CustomerId != userId && !IsStaffOrAdmin())
                {
                    throw new HubException("Bạn không có quyền gửi tin nhắn vào cuộc hội thoại này.");
                }
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"Conversation_{conversation.Id}");

            var message = await _chatService.SendMessageAsync(
                conversation.Id,
                userId,
                content.Trim(),
                ChatMessageType.Text,
                null);

            var fullMessage = await _context.ChatMessages
                .Include(m => m.Sender)
                .Include(m => m.Conversation)
                .FirstOrDefaultAsync(m => m.Id == message.Id);

            if (fullMessage != null)
            {
                await _notificationService.NotifyNewMessageAsync(fullMessage);
            }
        }

        public async Task MarkAsRead(int conversationId)
        {
            var userIdStr = Context.UserIdentifier ?? Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
            {
                return;
            }

            var conversation = await _context.ChatConversations
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == conversationId);

            if (conversation == null)
            {
                return;
            }

            if (conversation.CustomerId != userId && !IsStaffOrAdmin())
            {
                return;
            }

            await _chatService.MarkAsReadAsync(conversationId, userId);
        }

        public static bool IsUserOnline(string userId)
        {
            return OnlineUsers.ContainsKey(userId);
        }

        private bool IsStaffOrAdmin()
        {
            var user = Context.User;
            if (user == null) return false;

            return user.IsInRole(UserRoles.Admin) ||
                   user.IsInRole(UserRoles.Staff) ||
                   user.IsInRole(UserRoles.CustomerSupport) ||
                   user.IsInRole(UserRoles.WarehouseManager) ||
                   user.IsInRole(UserRoles.EmployeeManager);
        }
    }
}
