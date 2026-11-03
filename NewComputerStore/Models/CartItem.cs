namespace NewComputerStore.Models
{
    public class CartItem
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal OriginalPrice { get; set; }
        public decimal? DiscountPercent { get; set; }
        public int? ColorId { get; set; }
        public string ColorName { get; set; }
        public string ColorCode { get; set; }
        public string ImageUrl { get; set; }
        
        public decimal Total => Quantity * Price;
    }
}
