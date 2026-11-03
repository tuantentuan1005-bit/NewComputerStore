using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NewComputerStore.Data;
using NewComputerStore.Dtos;
using NewComputerStore.Models;

namespace NewComputerStore.Services
{
    public class OrderService : IOrderService
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;

        public OrderService(AppDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public (decimal OriginalPrice, decimal EffectiveDiscountPercent, decimal FinalPrice) CalculateProductPrice(
            Product product,
            string? voucherCode = null,
            DateTime? now = null)
        {
            var currentTime = now ?? DateTime.Now;

            bool isDiscountActive = product.DiscountPercent > 0
                && (!product.DiscountStartDate.HasValue || product.DiscountStartDate.Value <= currentTime)
                && (!product.DiscountEndDate.HasValue || product.DiscountEndDate.Value >= currentTime);

            if (isDiscountActive && !string.IsNullOrWhiteSpace(product.VoucherCode) && !string.IsNullOrWhiteSpace(voucherCode))
            {
                isDiscountActive = string.Equals(
                    product.VoucherCode.Trim(),
                    voucherCode.Trim(),
                    StringComparison.OrdinalIgnoreCase);
            }

            decimal effectiveDiscount = isDiscountActive ? product.DiscountPercent : 0m;
            decimal calculatedPrice = product.Price - (product.Price * effectiveDiscount / 100m);
            decimal finalPrice = Math.Max(0m, Math.Round(calculatedPrice, 0, MidpointRounding.AwayFromZero));

            return (product.Price, effectiveDiscount, finalPrice);
        }

        public async Task<(bool HasInvalidItems, Dictionary<string, string> ValidationErrors)> ValidateAndRefreshCartAsync(
            List<CartItem> cart)
        {
            var validationResults = new Dictionary<string, string>();
            bool hasInvalidItems = false;

            if (cart == null || cart.Count == 0)
            {
                return (false, validationResults);
            }

            var productIds = cart.Select(c => c.ProductId).Distinct().ToList();
            var dbProducts = await _context.Products
                .Include(p => p.ProductColors)
                .Where(p => productIds.Contains(p.ProductId))
                .ToListAsync();

            var now = DateTime.Now;

            foreach (var item in cart)
            {
                var prod = dbProducts.FirstOrDefault(p => p.ProductId == item.ProductId);
                string key = $"{item.ProductId}_{item.ColorId}";

                if (prod == null || prod.IsDeleted)
                {
                    validationResults[key] = "Sản phẩm không còn kinh doanh.";
                    hasInvalidItems = true;
                    continue;
                }

                if (prod.IsHidden)
                {
                    validationResults[key] = "Sản phẩm hiện đang tạm ẩn.";
                    hasInvalidItems = true;
                    continue;
                }

                // Refresh item price from DB
                var (originalPrice, effectiveDiscount, finalPrice) = CalculateProductPrice(prod, null, now);
                item.ProductName = prod.ProductName;
                item.OriginalPrice = originalPrice;
                item.DiscountPercent = effectiveDiscount;
                item.Price = finalPrice;

                if (item.ColorId.HasValue)
                {
                    var color = prod.ProductColors.FirstOrDefault(pc => pc.Id == item.ColorId.Value);
                    if (color == null)
                    {
                        validationResults[key] = "Màu sắc không hợp lệ.";
                        hasInvalidItems = true;
                    }
                    else
                    {
                        item.ColorName = color.ColorName;
                        item.ColorCode = color.ColorCode;
                        if (color.Quantity < item.Quantity)
                        {
                            validationResults[key] = $"Không đủ hàng tồn (Chỉ còn {color.Quantity}).";
                            hasInvalidItems = true;
                        }
                    }
                }
                else if (prod.Quantity < item.Quantity)
                {
                    validationResults[key] = $"Không đủ hàng tồn (Chỉ còn {prod.Quantity}).";
                    hasInvalidItems = true;
                }
            }

            return (hasInvalidItems, validationResults);
        }

        public async Task<OrderOperationResult> CreateOrderAsync(CreateOrderCommand command)
        {
            if (command.Items == null || command.Items.Count == 0)
            {
                return new OrderOperationResult
                {
                    Succeeded = false,
                    ErrorCode = "EMPTY_ORDER",
                    Message = "Đơn hàng phải có ít nhất 1 sản phẩm."
                };
            }

            if (command.Items.Any(i => i.Quantity <= 0))
            {
                return new OrderOperationResult
                {
                    Succeeded = false,
                    ErrorCode = "INVALID_QUANTITY",
                    Message = "Số lượng mỗi sản phẩm phải lớn hơn 0."
                };
            }

            var paymentMethod = string.Equals(command.PaymentMethod?.Trim(), "QR", StringComparison.OrdinalIgnoreCase)
                ? "QR"
                : "COD";

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var productIds = command.Items.Select(i => i.ProductId).Distinct().ToList();
                var dbProducts = await _context.Products
                    .Include(p => p.ProductColors)
                    .Where(p => productIds.Contains(p.ProductId))
                    .ToListAsync();

                // Validate voucherCode if client explicitly provided one
                var trimmedVoucher = string.IsNullOrWhiteSpace(command.VoucherCode)
                    ? null
                    : command.VoucherCode.Trim();

                if (trimmedVoucher != null)
                {
                    bool anyProductMatchesVoucher = dbProducts.Any(p =>
                        !string.IsNullOrWhiteSpace(p.VoucherCode) &&
                        string.Equals(p.VoucherCode.Trim(), trimmedVoucher, StringComparison.OrdinalIgnoreCase));

                    if (!anyProductMatchesVoucher)
                    {
                        await transaction.RollbackAsync();
                        return new OrderOperationResult
                        {
                            Succeeded = false,
                            ErrorCode = "INVALID_VOUCHER",
                            Message = $"Mã giảm giá '{trimmedVoucher}' không hợp lệ hoặc không áp dụng cho sản phẩm trong đơn hàng."
                        };
                    }
                }

                var now = DateTime.Now;
                var preparedDetails = new List<OrderDetail>();

                foreach (var item in command.Items)
                {
                    var prod = dbProducts.FirstOrDefault(p => p.ProductId == item.ProductId);
                    if (prod == null || prod.IsDeleted || prod.IsHidden)
                    {
                        await transaction.RollbackAsync();
                        return new OrderOperationResult
                        {
                            Succeeded = false,
                            ErrorCode = "PRODUCT_UNAVAILABLE",
                            Message = $"Sản phẩm (ID: {item.ProductId}) đã dừng bán hoặc tạm ẩn. Vui lòng cập nhật lại giỏ hàng."
                        };
                    }

                    ProductColor? selectedColor = null;
                    if (item.ColorId.HasValue)
                    {
                        selectedColor = prod.ProductColors.FirstOrDefault(pc => pc.Id == item.ColorId.Value);
                        if (selectedColor == null)
                        {
                            await transaction.RollbackAsync();
                            return new OrderOperationResult
                            {
                                Succeeded = false,
                                ErrorCode = "INVALID_COLOR",
                                Message = $"Màu sắc đã chọn cho sản phẩm '{prod.ProductName}' không hợp lệ."
                            };
                        }

                        if (selectedColor.Quantity < item.Quantity)
                        {
                            await transaction.RollbackAsync();
                            return new OrderOperationResult
                            {
                                Succeeded = false,
                                ErrorCode = "INSUFFICIENT_STOCK",
                                Message = $"Sản phẩm '{prod.ProductName}' (Màu: {selectedColor.ColorName}) không đủ tồn kho. Vui lòng cập nhật lại giỏ hàng."
                            };
                        }

                        selectedColor.Quantity -= item.Quantity;
                        _context.Entry(selectedColor).Property(c => c.RowVersion).OriginalValue = selectedColor.RowVersion;
                    }
                    else if (prod.ProductColors != null && prod.ProductColors.Any())
                    {
                        await transaction.RollbackAsync();
                        return new OrderOperationResult
                        {
                            Succeeded = false,
                            ErrorCode = "COLOR_REQUIRED",
                            Message = $"Vui lòng chọn màu sắc cho sản phẩm '{prod.ProductName}'."
                        };
                    }

                    if (prod.Quantity < item.Quantity)
                    {
                        await transaction.RollbackAsync();
                        return new OrderOperationResult
                        {
                            Succeeded = false,
                            ErrorCode = "INSUFFICIENT_STOCK",
                            Message = $"Sản phẩm '{prod.ProductName}' không đủ số lượng tồn kho. Vui lòng cập nhật lại giỏ hàng."
                        };
                    }

                    prod.Quantity -= item.Quantity;
                    _context.Entry(prod).Property(p => p.RowVersion).OriginalValue = prod.RowVersion;

                    var (originalPrice, effectiveDiscount, finalPrice) = CalculateProductPrice(prod, trimmedVoucher, now);

                    preparedDetails.Add(new OrderDetail
                    {
                        ProductId = prod.ProductId,
                        Quantity = item.Quantity,
                        OriginalPrice = originalPrice,
                        DiscountPercent = effectiveDiscount,
                        FinalPrice = finalPrice,
                        Price = finalPrice,
                        ColorName = selectedColor?.ColorName ?? "",
                        ColorCode = selectedColor?.ColorCode ?? ""
                    });
                }

                decimal totalPrice = preparedDetails.Sum(d => d.FinalPrice * d.Quantity);

                var order = new Order
                {
                    UserId = command.UserId,
                    OrderDate = now,
                    Status = "Pending",
                    FullName = command.FullName.Trim(),
                    Phone = command.Phone.Trim(),
                    Address = command.Address.Trim(),
                    TotalPrice = totalPrice,
                    PaymentMethod = paymentMethod,
                    PaymentStatus = "Pending",
                    Note = command.Note
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                foreach (var detail in preparedDetails)
                {
                    detail.OrderId = order.OrderId;
                    _context.OrderDetails.Add(detail);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Send OrderCreated email (non-blocking if SMTP fails)
                try
                {
                    var fullyPopulatedOrder = await _context.Orders
                        .Include(o => o.User)
                        .Include(o => o.OrderDetails)
                            .ThenInclude(od => od.Product)
                        .FirstOrDefaultAsync(o => o.OrderId == order.OrderId);

                    if (fullyPopulatedOrder != null)
                    {
                        var receiverEmail = !string.IsNullOrWhiteSpace(command.Email)
                            ? command.Email
                            : fullyPopulatedOrder.User?.Email;

                        if (!string.IsNullOrWhiteSpace(receiverEmail))
                        {
                            await _emailService.SendOrderStatusEmailAsync(receiverEmail, fullyPopulatedOrder, "Pending");
                        }
                    }

                    return new OrderOperationResult
                    {
                        Succeeded = true,
                        Message = "Đặt hàng thành công.",
                        Order = fullyPopulatedOrder ?? order
                    };
                }
                catch (Exception)
                {
                    return new OrderOperationResult
                    {
                        Succeeded = true,
                        Message = "Đặt hàng thành công.",
                        Order = order
                    };
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync();
                return new OrderOperationResult
                {
                    Succeeded = false,
                    IsConcurrencyConflict = true,
                    ErrorCode = "CONCURRENCY_CONFLICT",
                    Message = "Hệ thống đang bận do có nhiều người mua hàng cùng lúc. Vui lòng thử đặt hàng lại."
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return new OrderOperationResult
                {
                    Succeeded = false,
                    ErrorCode = "ORDER_CREATION_FAILED",
                    Message = $"Đặt hàng thất bại: {ex.Message}"
                };
            }
        }

        public async Task<List<Order>> GetUserOrdersAsync(int userId)
        {
            return await _context.Orders
                .AsNoTracking()
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
        }

        public async Task<Order?> GetOrderByIdWithDetailsAsync(int orderId)
        {
            return await _context.Orders
                .AsNoTracking()
                .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
        }

        public OrderPaymentQrDto BuildPaymentQrInfo(Order order, string baseUrl)
        {
            var transferContent = $"Thanh toan don hang #{order.OrderId}";
            var cleanBaseUrl = baseUrl.TrimEnd('/');

            return new OrderPaymentQrDto
            {
                OrderId = order.OrderId,
                Amount = order.TotalPrice,
                TransferContent = transferContent,
                QrImageUrl = $"{cleanBaseUrl}/images/qr.png",
                FallbackQrImageUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=220x220&data={Uri.EscapeDataString(transferContent)}",
                PaymentMethod = order.PaymentMethod,
                PaymentStatus = order.PaymentStatus,
                OrderStatus = order.Status
            };
        }
    }
}
