using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewComputerStore.Models
{
    public enum ConversationStatus
    {
        Open,
        Waiting,
        Answered,
        Closed
    }

    public class ChatConversation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int CustomerId { get; set; }

        [ForeignKey("CustomerId")]
        public virtual User Customer { get; set; }

        public int? StaffId { get; set; }

        [ForeignKey("StaffId")]
        public virtual User? Staff { get; set; }

        public int? OrderId { get; set; }

        [ForeignKey("OrderId")]
        public virtual Order? Order { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastMessageAt { get; set; } = DateTime.Now;

        public ConversationStatus Status { get; set; } = ConversationStatus.Open;

        public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }

    public enum ChatMessageType
    {
        Text,
        ProductCard,
        OrderCard,
        Image,
        System
    }

    public class ChatMessage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ConversationId { get; set; }

        [ForeignKey("ConversationId")]
        public virtual ChatConversation Conversation { get; set; }

        [Required]
        public int SenderId { get; set; }

        [ForeignKey("SenderId")]
        public virtual User Sender { get; set; }

        [Required]
        public string Content { get; set; }

        public ChatMessageType MessageType { get; set; } = ChatMessageType.Text;

        // JSON metadata for ProductCard, OrderCard, etc.
        public string? Metadata { get; set; }

        public DateTime SentAt { get; set; } = DateTime.Now;

        public bool IsRead { get; set; } = false;
    }

    public class QuickReply
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Content { get; set; }
        public int DisplayOrder { get; set; }
    }
}
