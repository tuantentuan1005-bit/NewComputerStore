using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NewComputerStore.Dtos;
using NewComputerStore.Models;
using NewComputerStore.Services;

namespace NewComputerStore.Api
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly IConfiguration _configuration;

        public OrdersController(IOrderService orderService, IConfiguration configuration)
        {
            _orderService = orderService;
            _configuration = configuration;
        }

        [HttpPost]
        [ProducesResponseType(typeof(OrderDetailDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequestDto model)
        {
            if (!TryGetUserId(out int userId))
            {
                return CreateProblem(
                    StatusCodes.Status401Unauthorized,
                    "Unauthorized",
                    "Token không chứa thông tin định danh người dùng hợp lệ.",
                    "UNAUTHORIZED");
            }

            var userEmail = !string.IsNullOrWhiteSpace(model.Email)
                ? model.Email
                : User.FindFirstValue(ClaimTypes.Email);

            var command = new CreateOrderCommand
            {
                UserId = userId,
                FullName = model.FullName,
                Email = userEmail,
                Phone = model.Phone,
                Address = model.Address,
                PaymentMethod = model.PaymentMethod,
                VoucherCode = model.VoucherCode,
                Note = model.Note,
                Items = model.Items.Select(i => new CreateOrderItemInput
                {
                    ProductId = i.ProductId,
                    ColorId = i.ColorId,
                    Quantity = i.Quantity
                }).ToList()
            };

            var result = await _orderService.CreateOrderAsync(command);
            if (!result.Succeeded || result.Order == null)
            {
                int statusCode = result.IsConcurrencyConflict
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status400BadRequest;

                string title = result.IsConcurrencyConflict
                    ? "Xung đột dữ liệu tồn kho"
                    : "Đặt hàng thất bại";

                return CreateProblem(
                    statusCode,
                    title,
                    result.Message,
                    result.ErrorCode ?? "ORDER_FAILED");
            }

            var dto = MapToOrderDetailDto(result.Order);
            return CreatedAtAction(nameof(GetOrderById), new { id = result.Order.OrderId }, dto);
        }

        [HttpGet]
        [ProducesResponseType(typeof(List<OrderSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetMyOrders()
        {
            if (!TryGetUserId(out int userId))
            {
                return CreateProblem(
                    StatusCodes.Status401Unauthorized,
                    "Unauthorized",
                    "Token không chứa thông tin định danh người dùng hợp lệ.",
                    "UNAUTHORIZED");
            }

            var orders = await _orderService.GetUserOrdersAsync(userId);
            var dtos = orders.Select(MapToOrderSummaryDto).ToList();

            return Ok(dtos);
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(OrderDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetOrderById(int id)
        {
            if (!TryGetUserId(out int userId))
            {
                return CreateProblem(
                    StatusCodes.Status401Unauthorized,
                    "Unauthorized",
                    "Token không chứa thông tin định danh người dùng hợp lệ.",
                    "UNAUTHORIZED");
            }

            var order = await _orderService.GetOrderByIdWithDetailsAsync(id);
            if (order == null)
            {
                return CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Không tìm thấy đơn hàng",
                    $"Không tìm thấy đơn hàng #{id}.",
                    "ORDER_NOT_FOUND");
            }

            if (order.UserId != userId)
            {
                return CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Truy cập bị từ chối",
                    "Bạn chỉ có quyền xem đơn hàng của chính mình.",
                    "FORBIDDEN");
            }

            return Ok(MapToOrderDetailDto(order));
        }

        [HttpGet("{id:int}/payment-qr")]
        [ProducesResponseType(typeof(OrderPaymentQrDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetOrderPaymentQr(int id)
        {
            if (!TryGetUserId(out int userId))
            {
                return CreateProblem(
                    StatusCodes.Status401Unauthorized,
                    "Unauthorized",
                    "Token không chứa thông tin định danh người dùng hợp lệ.",
                    "UNAUTHORIZED");
            }

            var order = await _orderService.GetOrderByIdWithDetailsAsync(id);
            if (order == null)
            {
                return CreateProblem(
                    StatusCodes.Status404NotFound,
                    "Không tìm thấy đơn hàng",
                    $"Không tìm thấy đơn hàng #{id}.",
                    "ORDER_NOT_FOUND");
            }

            if (order.UserId != userId)
            {
                return CreateProblem(
                    StatusCodes.Status403Forbidden,
                    "Truy cập bị từ chối",
                    "Bạn chỉ có quyền xem thông tin thanh toán đơn hàng của chính mình.",
                    "FORBIDDEN");
            }

            if (!string.Equals(order.PaymentMethod, "QR", StringComparison.OrdinalIgnoreCase))
            {
                return CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Phương thức thanh toán không hợp lệ",
                    "Đơn hàng này không sử dụng phương thức thanh toán QR.",
                    "INVALID_PAYMENT_METHOD");
            }

            var baseUrl = GetPublicBaseUrl();
            var qrDto = _orderService.BuildPaymentQrInfo(order, baseUrl);
            return Ok(qrDto);
        }

        private bool TryGetUserId(out int userId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userIdStr, out userId);
        }

        private OrderSummaryDto MapToOrderSummaryDto(Order order)
        {
            var items = (order.OrderDetails ?? Array.Empty<OrderDetail>())
                .Select(MapToOrderDetailItemDto)
                .ToList();

            return new OrderSummaryDto
            {
                OrderId = order.OrderId,
                OrderDate = order.OrderDate,
                Status = order.Status,
                TotalPrice = order.TotalPrice,
                PaymentMethod = order.PaymentMethod,
                PaymentStatus = order.PaymentStatus,
                FullName = order.FullName,
                Phone = order.Phone,
                Address = order.Address,
                TotalQuantity = items.Sum(i => i.Quantity),
                Items = items
            };
        }

        private OrderDetailDto MapToOrderDetailDto(Order order)
        {
            var baseUrl = GetPublicBaseUrl();
            var items = (order.OrderDetails ?? Array.Empty<OrderDetail>())
                .Select(MapToOrderDetailItemDto)
                .ToList();

            return new OrderDetailDto
            {
                OrderId = order.OrderId,
                OrderDate = order.OrderDate,
                Status = order.Status,
                TotalPrice = order.TotalPrice,
                PaymentMethod = order.PaymentMethod,
                PaymentStatus = order.PaymentStatus,
                FullName = order.FullName,
                Phone = order.Phone,
                Address = order.Address,
                Note = order.Note,
                Items = items,
                PaymentQr = string.Equals(order.PaymentMethod, "QR", StringComparison.OrdinalIgnoreCase)
                    ? _orderService.BuildPaymentQrInfo(order, baseUrl)
                    : null
            };
        }

        private OrderDetailItemDto MapToOrderDetailItemDto(OrderDetail od)
        {
            return new OrderDetailItemDto
            {
                OrderDetailId = od.OrderDetailId,
                ProductId = od.ProductId,
                ProductName = od.Product?.ProductName ?? "Sản phẩm",
                ProductImageUrl = ToAbsoluteImageUrl(od.Product?.ImageUrl),
                Quantity = od.Quantity,
                OriginalPrice = od.OriginalPrice,
                DiscountPercent = od.DiscountPercent,
                FinalPrice = od.FinalPrice,
                LineTotal = od.Quantity * od.FinalPrice,
                ColorName = od.ColorName,
                ColorCode = od.ColorCode
            };
        }

        private string GetPublicBaseUrl()
        {
            var configured = _configuration["PublicBaseUrl"]?.Trim().TrimEnd('/');
            if (!string.IsNullOrEmpty(configured))
            {
                return configured;
            }

            return $"{Request.Scheme}://{Request.Host}";
        }

        private string? ToAbsoluteImageUrl(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            var trimmed = fileName.Trim();
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            return $"{GetPublicBaseUrl()}/images/{trimmed.TrimStart('/')}";
        }

        private ObjectResult CreateProblem(int statusCode, string title, string detail, string code)
        {
            var problem = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = Request.Path
            };
            problem.Extensions["code"] = code;

            return new ObjectResult(problem)
            {
                StatusCode = statusCode
            };
        }
    }
}
