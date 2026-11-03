using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewComputerStore.Data;
using NewComputerStore.Dtos;
using NewComputerStore.Models;
using NewComputerStore.Services;

namespace NewComputerStore.Api
{
    [ApiController]
    [Route("api/chat")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly INotificationService _notificationService;
        private readonly AppDbContext _context;

        public ChatController(
            IChatService chatService,
            INotificationService notificationService,
            AppDbContext context)
        {
            _chatService = chatService;
            _notificationService = notificationService;
            _context = context;
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory(
            [FromQuery] int? orderId = null,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 50)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
            {
                return CreateProblem(StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Không xác định được người dùng từ token.");
            }

            if (skip < 0) skip = 0;
            if (take <= 0) take = 50;
            if (take > 200) take = 200;

            var conversation = await _chatService.GetOrCreateConversationAsync(userId, orderId);
            var messages = await _chatService.GetMessagesAsync(conversation.Id, skip, take);
            await _chatService.MarkAsReadAsync(conversation.Id, userId);

            var response = new ChatHistoryResponseDto
            {
                ConversationId = conversation.Id,
                CustomerId = conversation.CustomerId,
                OrderId = conversation.OrderId,
                Status = conversation.Status.ToString(),
                StaffId = conversation.StaffId,
                StaffName = conversation.Staff?.FullName ?? "Nhân viên hỗ trợ",
                CreatedAt = conversation.CreatedAt,
                LastMessageAt = conversation.LastMessageAt,
                Messages = messages.Select(m => new ChatMessageDto
                {
                    Id = m.Id,
                    ConversationId = m.ConversationId,
                    SenderId = m.SenderId,
                    SenderName = m.Sender?.FullName ?? "Người dùng",
                    Content = m.Content,
                    Type = m.MessageType.ToString(),
                    Metadata = m.Metadata,
                    SentAt = m.SentAt.ToString("HH:mm"),
                    SentAtFull = m.SentAt,
                    IsRead = m.SenderId == userId ? m.IsRead : true,
                    IsStaff = m.SenderId != conversation.CustomerId
                }).ToList()
            };

            return Ok(response);
        }

        [HttpPost("messages")]
        public async Task<IActionResult> SendMessage([FromBody] SendChatMessageRequestDto request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
            {
                return CreateProblem(StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Không xác định được người dùng từ token.");
            }

            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return CreateProblem(StatusCodes.Status400BadRequest, "EMPTY_MESSAGE", "Nội dung tin nhắn không được để trống.");
            }

            ChatConversation? conversation;
            if (request.ConversationId.HasValue && request.ConversationId.Value > 0)
            {
                conversation = await _context.ChatConversations.FindAsync(request.ConversationId.Value);
                if (conversation == null)
                {
                    return CreateProblem(StatusCodes.Status404NotFound, "CONVERSATION_NOT_FOUND", "Không tìm thấy cuộc hội thoại.");
                }

                if (conversation.CustomerId != userId && !IsStaffOrAdmin())
                {
                    return CreateProblem(StatusCodes.Status403Forbidden, "FORBIDDEN", "Bạn không có quyền gửi tin nhắn vào cuộc hội thoại này.");
                }
            }
            else
            {
                conversation = await _chatService.GetOrCreateConversationAsync(userId, request.OrderId);
            }

            var msgType = ChatMessageType.Text;
            if (!string.IsNullOrWhiteSpace(request.Type) &&
                Enum.TryParse<ChatMessageType>(request.Type, true, out var parsedType))
            {
                msgType = parsedType;
            }

            var message = await _chatService.SendMessageAsync(
                conversation.Id,
                userId,
                request.Content.Trim(),
                msgType,
                request.Metadata);

            var fullMessage = await _context.ChatMessages
                .Include(m => m.Sender)
                .Include(m => m.Conversation)
                .FirstOrDefaultAsync(m => m.Id == message.Id);

            if (fullMessage != null)
            {
                await _notificationService.NotifyNewMessageAsync(fullMessage);
            }

            var dto = new ChatMessageDto
            {
                Id = message.Id,
                ConversationId = message.ConversationId,
                SenderId = message.SenderId,
                SenderName = fullMessage?.Sender?.FullName ?? User.FindFirstValue(ClaimTypes.Name) ?? "Người dùng",
                Content = message.Content,
                Type = message.MessageType.ToString(),
                Metadata = message.Metadata,
                SentAt = message.SentAt.ToString("HH:mm"),
                SentAtFull = message.SentAt,
                IsRead = message.IsRead,
                IsStaff = message.SenderId != conversation.CustomerId
            };

            return Ok(dto);
        }

        [HttpPost("read")]
        public async Task<IActionResult> MarkRead([FromBody] MarkChatReadRequestDto request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
            {
                return CreateProblem(StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Không xác định được người dùng từ token.");
            }

            var conversation = await _context.ChatConversations
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.ConversationId);

            if (conversation == null)
            {
                return CreateProblem(StatusCodes.Status404NotFound, "CONVERSATION_NOT_FOUND", "Không tìm thấy cuộc hội thoại.");
            }

            if (conversation.CustomerId != userId && !IsStaffOrAdmin())
            {
                return CreateProblem(StatusCodes.Status403Forbidden, "FORBIDDEN", "Bạn không có quyền truy cập cuộc hội thoại này.");
            }

            await _chatService.MarkAsReadAsync(request.ConversationId, userId);
            return Ok(new { message = "Đã đánh dấu đã đọc." });
        }

        private bool IsStaffOrAdmin()
        {
            return User.IsInRole(UserRoles.Admin) ||
                   User.IsInRole(UserRoles.Staff) ||
                   User.IsInRole(UserRoles.CustomerSupport) ||
                   User.IsInRole(UserRoles.WarehouseManager) ||
                   User.IsInRole(UserRoles.EmployeeManager);
        }

        private ObjectResult CreateProblem(int statusCode, string code, string detail)
        {
            var problem = new ProblemDetails
            {
                Status = statusCode,
                Title = statusCode switch
                {
                    StatusCodes.Status400BadRequest => "Bad Request",
                    StatusCodes.Status401Unauthorized => "Unauthorized",
                    StatusCodes.Status403Forbidden => "Forbidden",
                    StatusCodes.Status404NotFound => "Not Found",
                    _ => "Error"
                },
                Detail = detail,
                Instance = HttpContext.Request.Path
            };
            problem.Extensions["code"] = code;
            return StatusCode(statusCode, problem);
        }
    }
}
