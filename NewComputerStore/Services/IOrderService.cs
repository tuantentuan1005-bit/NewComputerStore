using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NewComputerStore.Dtos;
using NewComputerStore.Models;

namespace NewComputerStore.Services
{
    public class CreateOrderItemInput
    {
        public int ProductId { get; set; }
        public int? ColorId { get; set; }
        public int Quantity { get; set; }
    }

    public class CreateOrderCommand
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = "COD";
        public string? VoucherCode { get; set; }
        public string? Note { get; set; }
        public List<CreateOrderItemInput> Items { get; set; } = new List<CreateOrderItemInput>();
    }

    public class OrderOperationResult
    {
        public bool Succeeded { get; set; }
        public bool IsConcurrencyConflict { get; set; }
        public string? ErrorCode { get; set; }
        public string Message { get; set; } = string.Empty;
        public Order? Order { get; set; }
    }

    public interface IOrderService
    {
        (decimal OriginalPrice, decimal EffectiveDiscountPercent, decimal FinalPrice) CalculateProductPrice(
            Product product,
            string? voucherCode = null,
            DateTime? now = null);

        Task<(bool HasInvalidItems, Dictionary<string, string> ValidationErrors)> ValidateAndRefreshCartAsync(
            List<CartItem> cart);

        Task<OrderOperationResult> CreateOrderAsync(CreateOrderCommand command);

        Task<List<Order>> GetUserOrdersAsync(int userId);

        Task<Order?> GetOrderByIdWithDetailsAsync(int orderId);

        OrderPaymentQrDto BuildPaymentQrInfo(Order order, string baseUrl);
    }
}
