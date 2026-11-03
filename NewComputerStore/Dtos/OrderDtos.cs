using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NewComputerStore.Dtos
{
    public class CreateOrderItemRequestDto
    {
        [Required]
        public int ProductId { get; set; }

        public int? ColorId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Số lượng phải từ 1 trở lên.")]
        public int Quantity { get; set; }
    }

    public class CreateOrderRequestDto
    {
        [Required(ErrorMessage = "Danh sách sản phẩm không được để trống.")]
        [MinLength(1, ErrorMessage = "Đơn hàng phải có ít nhất 1 sản phẩm.")]
        public List<CreateOrderItemRequestDto> Items { get; set; } = new List<CreateOrderItemRequestDto>();

        [Required(ErrorMessage = "Họ tên người nhận không được để trống.")]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Số điện thoại người nhận không được để trống.")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        [StringLength(20)]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Địa chỉ giao hàng không được để trống.")]
        [StringLength(255)]
        public string Address { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ.")]
        public string? Email { get; set; }

        [StringLength(50)]
        public string PaymentMethod { get; set; } = "COD"; // "COD" | "QR"

        [StringLength(50)]
        public string? VoucherCode { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }
    }

    public class OrderDetailItemDto
    {
        public int OrderDetailId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ProductImageUrl { get; set; }
        public int Quantity { get; set; }
        public decimal OriginalPrice { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal FinalPrice { get; set; }
        public decimal LineTotal { get; set; }
        public string? ColorName { get; set; }
        public string? ColorCode { get; set; }
    }

    public class OrderSummaryDto
    {
        public int OrderId { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public int TotalQuantity { get; set; }
        public List<OrderDetailItemDto> Items { get; set; } = new List<OrderDetailItemDto>();
    }

    public class OrderDetailDto
    {
        public int OrderId { get; set; }
        public DateTime OrderDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string? Note { get; set; }
        public List<OrderDetailItemDto> Items { get; set; } = new List<OrderDetailItemDto>();
        public OrderPaymentQrDto? PaymentQr { get; set; }
    }

    public class OrderPaymentQrDto
    {
        public int OrderId { get; set; }
        public decimal Amount { get; set; }
        public string TransferContent { get; set; } = string.Empty;
        public string QrImageUrl { get; set; } = string.Empty;
        public string FallbackQrImageUrl { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = string.Empty;
        public string OrderStatus { get; set; } = string.Empty;
    }
}
