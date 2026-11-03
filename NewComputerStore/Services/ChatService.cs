using Microsoft.EntityFrameworkCore;
using NewComputerStore.Data;
using NewComputerStore.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NewComputerStore.Services
{
    public interface IChatService
    {
        Task<ChatConversation> GetOrCreateConversationAsync(int customerId, int? orderId = null);
        Task<ChatMessage> SendMessageAsync(int conversationId, int senderId, string content, ChatMessageType type = ChatMessageType.Text, string? metadata = null);
        Task<List<ChatMessage>> GetMessagesAsync(int conversationId, int skip = 0, int take = 50);
        Task<List<ChatConversation>> GetActiveConversationsAsync(ConversationStatus? status = null);
        Task MarkAsReadAsync(int conversationId, int userId);
        Task UpdateConversationStatusAsync(int conversationId, ConversationStatus status);
        Task AssignStaffAsync(int conversationId, int staffId);
        Task<int> GetUnreadCountAsync(int userId);
    }

    public class ChatService : IChatService
    {
        private readonly AppDbContext _context;

        public ChatService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ChatConversation> GetOrCreateConversationAsync(int customerId, int? orderId = null)
        {
            var conversation = await _context.ChatConversations
                .Include(c => c.Customer)
                .Include(c => c.Staff)
                .Include(c => c.Order)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId && c.Status != ConversationStatus.Closed && c.OrderId == orderId);

            if (conversation == null)
            {
                conversation = new ChatConversation
                {
                    CustomerId = customerId,
                    OrderId = orderId,
                    Status = ConversationStatus.Open
                };
                _context.ChatConversations.Add(conversation);
                await _context.SaveChangesAsync();
            }

            return conversation;
        }

        public async Task<ChatMessage> SendMessageAsync(int conversationId, int senderId, string content, ChatMessageType type = ChatMessageType.Text, string? metadata = null)
        {
            var conversation = await _context.ChatConversations.FindAsync(conversationId);
            if (conversation == null) throw new Exception("Conversation not found");

            var message = new ChatMessage
            {
                ConversationId = conversationId,
                SenderId = senderId,
                Content = content,
                MessageType = type,
                Metadata = metadata,
                SentAt = DateTime.Now
            };

            _context.ChatMessages.Add(message);
            
            conversation.LastMessageAt = DateTime.Now;
            // If sender is customer of this conversation, set status to Waiting for staff
            // If sender is staff (anyone other than the customer), set status to Answered
            if (senderId != conversation.CustomerId)
            {
                conversation.Status = ConversationStatus.Answered;
                if (conversation.StaffId == null) conversation.StaffId = senderId;
            }
            else
            {
                conversation.Status = ConversationStatus.Waiting;
            }

            await _context.SaveChangesAsync();
            return message;
        }

        public async Task<List<ChatMessage>> GetMessagesAsync(int conversationId, int skip = 0, int take = 50)
        {
            return await _context.ChatMessages
                .Include(m => m.Sender)
                .Where(m => m.ConversationId == conversationId)
                .OrderByDescending(m => m.SentAt)
                .Skip(skip)
                .Take(take)
                .OrderBy(m => m.SentAt)
                .ToListAsync();
        }

        public async Task<List<ChatConversation>> GetActiveConversationsAsync(ConversationStatus? status = null)
        {
            var query = _context.ChatConversations
                .Include(c => c.Customer)
                .Include(c => c.Order)
                .Include(c => c.Messages.OrderByDescending(m => m.SentAt).Take(1))
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(c => c.Status == status.Value);
            }
            else
            {
                query = query.Where(c => c.Status != ConversationStatus.Closed);
            }

            return await query.OrderByDescending(c => c.LastMessageAt).ToListAsync();
        }

        public async Task MarkAsReadAsync(int conversationId, int userId)
        {
            var messages = await _context.ChatMessages
                .Where(m => m.ConversationId == conversationId && m.SenderId != userId && !m.IsRead)
                .ToListAsync();

            foreach (var m in messages)
            {
                m.IsRead = true;
            }
            await _context.SaveChangesAsync();
        }

        public async Task UpdateConversationStatusAsync(int conversationId, ConversationStatus status)
        {
            var conversation = await _context.ChatConversations.FindAsync(conversationId);
            if (conversation != null)
            {
                conversation.Status = status;
                await _context.SaveChangesAsync();
            }
        }

        public async Task AssignStaffAsync(int conversationId, int staffId)
        {
            var conversation = await _context.ChatConversations.FindAsync(conversationId);
            if (conversation != null)
            {
                conversation.StaffId = staffId;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            return await _context.ChatMessages
                .Include(m => m.Conversation)
                .Where(m => m.Conversation.CustomerId == userId && m.SenderId != userId && !m.IsRead)
                .CountAsync();
        }
    }
}
