using System;

namespace NewComputerStore.ViewModels
{
    public class SlowSellingProductViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string? ImageUrl { get; set; }
        public string CategoryName { get; set; }
        public decimal Price { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal FinalPrice => Math.Max(0, Math.Round(Price - (Price * DiscountPercent / 100), 0, MidpointRounding.AwayFromZero));
        public int TotalSold { get; set; }
        public int StockQuantity { get; set; }
        public DateTime? LastSoldDate { get; set; }
        public int DaysSinceLastSale { get; set; }
        public DateTime? LastViewedDate { get; set; }
        public decimal InventoryValue { get; set; }
        public int ViewCount { get; set; }
        public string Status { get; set; } // Ế nặng, Bán chậm, Tồn kho nguy hiểm, Dead stock, Ổn định
        public string SuggestedPromotion { get; set; } // Flash Sale -20%, Đẩy quảng cáo, Combo khuyến mãi, Xả kho, v.v.
        public string SuggestedReason { get; set; } // Lý do đề xuất khuyến mãi
        public double HeatScore { get; set; }
        public double ConversionRate { get; set; }
        public string ConversionRateText { get; set; } // Rất kém (<1%), Trung bình (1-3%), Tốt (>3%)
        public bool IsHidden { get; set; }
        public bool IsDeleted { get; set; }
    }
}
