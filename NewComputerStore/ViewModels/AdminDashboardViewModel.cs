namespace NewComputerStore.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int TotalProducts { get; set; }
        public int TotalOrders { get; set; }
        public int TotalUsers { get; set; }

        public decimal TotalRevenue { get; set; }
        public decimal RevenueToday { get; set; }
        public decimal RevenueThisMonth { get; set; }
        public decimal RevenueLastMonth { get; set; }
        public double RevenueGrowth { get; set; }

        public System.Collections.Generic.List<NewComputerStore.Models.Order> PaidOrders { get; set; }

        public System.Collections.Generic.List<string> ChartLabels7Days { get; set; }
        public System.Collections.Generic.List<decimal> ChartData7Days { get; set; }

        public System.Collections.Generic.List<string> ChartLabelsMonths { get; set; }
        public System.Collections.Generic.List<decimal> ChartDataMonths { get; set; }

        public System.Collections.Generic.List<ItemSales> TopSellingProducts { get; set; }
        public System.Collections.Generic.List<ItemSales> LeastSellingProducts { get; set; }
        public System.Collections.Generic.List<ItemSales> DeadStockProducts { get; set; }

        // --- Email Statistics ---
        public int TotalEmailsSent { get; set; }
        public int TotalEmailsSuccess { get; set; }
        public int TotalEmailsFailed { get; set; }
        public int TotalEmailsSkipped { get; set; }
        public double EmailVerificationRate { get; set; }
        public int UnverifiedUsersCount { get; set; }

        public System.Collections.Generic.List<string> ChartLabels30Days { get; set; }
        public System.Collections.Generic.List<int> ChartSuccess30Days { get; set; }
        public System.Collections.Generic.List<int> ChartFailed30Days { get; set; }
    }

    public class ItemSales
    {
        public string ProductName { get; set; }
        public int TotalSold { get; set; }
    }
}
