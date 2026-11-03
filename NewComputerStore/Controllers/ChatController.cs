using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewComputerStore.Models;
using NewComputerStore.Services;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;
using NewComputerStore.Data;
using Microsoft.EntityFrameworkCore;

namespace NewComputerStore.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly IChatService _chatService;
        private readonly INotificationService _notificationService;
        private readonly AppDbContext _context;

        public ChatController(IChatService chatService, INotificationService notificationService, AppDbContext context)
        {
            _chatService = chatService;
            _notificationService = notificationService;
            _context = context;
        }

        // --- Customer Actions ---

        [HttpGet]
        public async Task<IActionResult> GetConversation(int? orderId = null)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var conversation = await _chatService.GetOrCreateConversationAsync(userId, orderId);
            return Ok(new { 
                id = conversation.Id, 
                status = conversation.Status.ToString(),
                staffName = conversation.Staff?.FullName ?? "Nhân viên hỗ trợ"
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetMessages(int conversationId, int skip = 0)
        {
            var conversation = await _context.ChatConversations.FindAsync(conversationId);
            if (conversation == null) return NotFound();

            var messages = await _chatService.GetMessagesAsync(conversationId, skip);
            var result = messages.Select(m => new {
                id = m.Id,
                senderId = m.SenderId,
                senderName = m.Sender?.FullName,
                content = m.Content,
                type = m.MessageType.ToString(),
                metadata = m.Metadata,
                sentAt = m.SentAt.ToString("HH:mm"),
                isStaff = m.SenderId != conversation.CustomerId
            });
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage(int conversationId, string content, ChatMessageType type = ChatMessageType.Text, string? metadata = null)
        {
            if (string.IsNullOrWhiteSpace(content)) return BadRequest();

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var message = await _chatService.SendMessageAsync(conversationId, userId, content, type, metadata);
            
            // Reload message with Sender and Conversation info for notification
            message = await _context.ChatMessages
                .Include(m => m.Sender)
                .Include(m => m.Conversation)
                .FirstOrDefaultAsync(m => m.Id == message.Id);
            
            await _notificationService.NotifyNewMessageAsync(message);
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> MarkRead(int conversationId)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            await _chatService.MarkAsReadAsync(conversationId, userId);
            return Ok();
        }

        // --- Admin/Staff Actions ---

        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff + "," + UserRoles.WarehouseManager + "," + UserRoles.EmployeeManager + "," + UserRoles.CustomerSupport)]
        public IActionResult StaffDashboard()
        {
            ViewData["Title"] = "Tư vấn khách hàng";
            return View();
        }

        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff + "," + UserRoles.WarehouseManager + "," + UserRoles.EmployeeManager + "," + UserRoles.CustomerSupport)]
        [HttpGet]
        public async Task<IActionResult> GetConversations(ConversationStatus? status = null)
        {
            var conversations = await _chatService.GetActiveConversationsAsync(status);
            var result = conversations.Select(c => new {
                id = c.Id,
                customerId = c.CustomerId,
                customerName = c.Customer.FullName,
                lastMessage = c.Messages.OrderByDescending(m => m.SentAt).FirstOrDefault()?.Content ?? "Bắt đầu hội thoại",
                lastMessageAt = c.LastMessageAt.ToString("HH:mm dd/MM"),
                status = c.Status.ToString(),
                unreadCount = c.Messages.Count(m => !m.IsRead && m.SenderId != int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier))),
                orderId = c.OrderId
            });
            return Ok(result);
        }

        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff + "," + UserRoles.WarehouseManager + "," + UserRoles.EmployeeManager + "," + UserRoles.CustomerSupport)]
        [HttpGet]
        public async Task<IActionResult> GetCustomerInfo(int customerId)
        {
            var customer = await _context.Users
                .Include(u => u.Orders)
                    .ThenInclude(o => o.OrderDetails)
                        .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(u => u.UserId == customerId);

            if (customer == null) return NotFound();

            var recentOrders = customer.Orders.OrderByDescending(o => o.OrderDate).Take(5).Select(o => new {
                id = o.OrderId,
                date = o.OrderDate.ToString("dd/MM/yyyy"),
                total = o.TotalPrice.ToString("N0") + "₫",
                status = o.Status
            });

            var totalSpent = customer.Orders.Where(o => o.Status == "Delivered").Sum(o => o.TotalPrice);
            var totalOrders = customer.Orders.Count;
            var purchasedProducts = customer.Orders
                .SelectMany(o => o.OrderDetails)
                .Select(od => od.Product)
                .Where(p => p != null)
                .Distinct()
                .Take(5)
                .Select(p => new {
                    id = p.ProductId,
                    name = p.ProductName,
                    image = p.ImageUrl
                });

            return Ok(new {
                name = customer.FullName,
                email = customer.Email,
                phone = customer.Orders.OrderByDescending(o => o.OrderDate).FirstOrDefault()?.Phone ?? "Chưa có SĐT",
                totalSpent = totalSpent.ToString("N0") + "₫",
                totalOrders = totalOrders,
                orders = recentOrders,
                products = purchasedProducts
            });
        }

        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff + "," + UserRoles.WarehouseManager + "," + UserRoles.EmployeeManager + "," + UserRoles.CustomerSupport)]
        [HttpPost]
        public async Task<IActionResult> CloseConversation(int id)
        {
            await _chatService.UpdateConversationStatusAsync(id, ConversationStatus.Closed);
            var conversation = await _context.ChatConversations.FindAsync(id);
            await _notificationService.NotifyConversationUpdatedAsync(conversation);
            return Ok();
        }

        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff + "," + UserRoles.WarehouseManager + "," + UserRoles.EmployeeManager + "," + UserRoles.CustomerSupport)]
        [HttpGet]
        public async Task<IActionResult> GetQuickReplies()
        {
            var replies = await _context.QuickReplies.OrderBy(q => q.DisplayOrder).ToListAsync();
            if (!replies.Any())
            {
                // Seed some defaults
                replies = new List<QuickReply> {
                    new QuickReply { Content = "Chào bạn, shop có thể giúp gì cho bạn?", DisplayOrder = 1 },
                    new QuickReply { Content = "Sản phẩm này hiện đang còn hàng ạ.", DisplayOrder = 2 },
                    new QuickReply { Content = "Shop sẽ kiểm tra và phản hồi bạn ngay lập tức.", DisplayOrder = 3 },
                    new QuickReply { Content = "Cảm ơn bạn đã quan tâm đến sản phẩm của shop!", DisplayOrder = 4 }
                };
                _context.QuickReplies.AddRange(replies);
                await _context.SaveChangesAsync();
            }
            return Ok(replies);
        }

        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff + "," + UserRoles.WarehouseManager + "," + UserRoles.EmployeeManager + "," + UserRoles.CustomerSupport)]
        [HttpGet]
        public async Task<IActionResult> SearchProducts(string term)
        {
            var products = await _context.Products
                .Where(p => p.ProductName.Contains(term) && !p.IsHidden && !p.IsDeleted)
                .Take(10)
                .Select(p => new {
                    id = p.ProductId,
                    name = p.ProductName,
                    price = p.Price.ToString("N0") + "₫",
                    image = p.ImageUrl
                })
                .ToListAsync();
            return Ok(products);
        }
    }
}
