using System;
using System.Collections.Generic;

namespace NewComputerStore.Dtos
{
    public class ChatMessageDto
    {
        public int Id { get; set; }
        public int ConversationId { get; set; }
        public int SenderId { get; set; }
        public string SenderName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? Metadata { get; set; }
        public string SentAt { get; set; } = string.Empty;
        public DateTime SentAtFull { get; set; }
        public bool IsRead { get; set; }
        public bool IsStaff { get; set; }
    }

    public class ChatHistoryResponseDto
    {
        public int ConversationId { get; set; }
        public int CustomerId { get; set; }
        public int? OrderId { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? StaffId { get; set; }
        public string StaffName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastMessageAt { get; set; }
        public List<ChatMessageDto> Messages { get; set; } = new();
    }

    public class SendChatMessageRequestDto
    {
        public int? ConversationId { get; set; }
        public int? OrderId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string? Type { get; set; }
        public string? Metadata { get; set; }
    }

    public class MarkChatReadRequestDto
    {
        public int ConversationId { get; set; }
    }
}
