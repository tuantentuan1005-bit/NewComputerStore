using System;
using System.Collections.Generic;

namespace NewComputerStore.Helpers
{
    public static class LocalizationHelper
    {
        private static string _currentLanguage = "vi-VN";

        private static readonly Dictionary<string, string> _vnTranslations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // Menus & Modules
            { "Dashboard", "Doanh thu" },
            { "Orders", "Đơn hàng" },
            { "Products", "Sản phẩm" },
            { "Categories", "Danh mục" },
            { "Category", "Loại sản phẩm" },
            { "Users", "Người dùng" },
            { "Customers", "Khách hàng" },
            { "Staff", "Nhân viên" },
            { "Activity Logs", "Nhật ký hoạt động" },

            // Dashboard specifics
            { "Revenue 7 Days", "Doanh thu 7 ngày gần nhất" },
            { "Revenue By Month", "Doanh thu theo tháng" },
            { "Top 5 Best Selling", "Top 5 sản phẩm bán chạy" },
            { "Top 5 Least Selling", "Top 5 sản phẩm bán kém" },
            { "Dead Stock", "Sản phẩm chưa bán" },
            { "Paid Orders List", "Danh sách đơn đã ghi nhận doanh thu" },
            { "Revenue Today", "Doanh thu hôm nay" },
            { "Revenue This Month", "Doanh thu tháng này" },
            { "Total Revenue", "Tổng doanh thu" },
            { "Compared to last month", "So với tháng trước" },
            
            // Table Headers
            { "Order ID", "Mã đơn" },
            { "Customer", "Khách hàng" },
            { "Payment Method", "Phương thức thanh toán" },
            { "Total", "Tổng tiền" },
            { "Created Date", "Ngày tạo" },
            { "Status", "Trạng thái" },
            { "Quantity", "Số lượng" },
            { "Product Name", "Tên sản phẩm" },
            { "Total Price", "Tổng tiền" },
            { "Date", "Ngày đặt" },
            { "Action", "Thao tác" },
            { "Order Details", "Chi tiết đơn hàng" },
            { "Order Items", "Sản phẩm trong đơn" },
            { "Shipping Information", "Thông tin giao hàng" },
            { "Receiver", "Người nhận" },
            { "Delivery Address", "Địa chỉ giao hàng" },
            { "Order Summary", "Tóm tắt đơn hàng" },
            { "Subtotal", "Tạm tính" },
            { "Shipping", "Phí vận chuyển" },
            { "FREE", "MIỄN PHÍ" },
            { "Total Paid", "Tổng thanh toán" },
            { "Phone Number", "Số điện thoại" },
            { "Order Date", "Ngày đặt hàng" },
            { "Back to My Orders", "Quay lại đơn hàng của tôi" },
            { "Need help with your order?", "Bạn cần hỗ trợ về đơn hàng?" },
            { "Product Unavailable", "Sản phẩm không khả dụng" },
            { "Checkout", "Thanh toán" },
            { "Receiver's Full Name", "Họ tên người nhận" },
            { "Enter full name", "Nhập họ tên" },
            { "Email Address", "Địa chỉ Email" },
            { "Used for order confirmation.", "Dùng để xác nhận đơn hàng." },
            { "Enter your full street address, city, etc.", "Nhập địa chỉ chi tiết (số nhà, tên đường, quận/huyện...)" },
            { "Place Order", "Đặt hàng" },
            { "Qty:", "SL:" },
            { "Shipping Fee", "Phí vận chuyển" },
            { "Grand Total", "Tổng cộng" },
            { "Back to Cart", "Quay lại giỏ hàng" },
            { "Order Success", "Đặt hàng thành công" },
            { "Order Placed Successfully!", "Đặt hàng thành công!" },
            { "Thank you for your purchase.", "Cảm ơn bạn đã mua sắm tại cửa hàng." },
            { "Your Order ID is:", "Mã đơn hàng của bạn là:" },
            { "View Order Details", "Xem chi tiết đơn hàng" },
            { "QR Payment", "Thanh Toán QR" },
            { "Scan QR code to pay for your order", "Quét mã QR để thanh toán đơn hàng" },
            { "Amount to transfer", "Số tiền cần chuyển" },
            { "Transfer Information", "Thông tin chuyển khoản" },
            { "Transfer Content:", "Nội dung chuyển khoản:" },
            { "Amount:", "Số tiền:" },
            { "I have paid", "Tôi đã thanh toán" },
            { "All Products", "Tất cả sản phẩm" },
            { "Find the perfect component for your next build", "Tìm kiếm linh kiện hoàn hảo cho bộ máy của bạn" },
            { "Filters", "Bộ lọc" },
            { "Real-time Search", "Tìm kiếm nhanh" },
            { "Type to filter...", "Gõ để lọc..." },
            { "Filters items on this page instantly.", "Lọc các sản phẩm hiện có trên trang này." },
            { "Global Search", "Tìm kiếm hệ thống" },
            { "Search all products...", "Tìm kiếm tất cả sản phẩm..." },
            { "All Categories", "Tất cả danh mục" },
            { "Apply Filters", "Áp dụng lọc" },
            { "Reset", "Thiết lập lại" },
            { "No products found matching your criteria.", "Không tìm thấy sản phẩm nào phù hợp với tiêu chí của bạn." },
            { "No matches found on this page.", "Không tìm thấy kết quả nào trên trang này." },
            { "Contact our support at", "Liên hệ hỗ trợ tại" },
            { "My Orders", "Đơn hàng của tôi" },
            { "Order", "Đơn hàng" },
            { "Admin Orders", "Quản lý đơn hàng (Admin)" },
            { "All Orders", "Tất cả đơn hàng" },
            { "Start shopping now!", "Bắt đầu mua sắm ngay!" },
            { "You haven't placed any orders yet.", "Bạn chưa có đơn hàng nào." },

            // Status
            { "Pending", "Chờ xử lý" },
            { "Processing", "Đang xử lý" },
            { "Shipped", "Đang giao" },
            { "Completed", "Hoàn thành" },
            { "Cancelled", "Đã hủy" },
            { "Delivered", "Đã giao hàng" },

            // Payment
            { "QR (Paid)", "QR (Đã thanh toán)" },
            { "COD", "Thanh toán khi nhận hàng" },
            { "QR", "Thanh toán QR" },

            // Buttons & Actions
            { "Create", "Thêm mới" },
            { "Create New", "Thêm mới" },
            { "Edit", "Chỉnh sửa" },
            { "Delete", "Xóa" },
            { "Save", "Lưu" },
            { "Cancel", "Hủy" },
            { "Submit", "Xác nhận" },
            { "Update", "Cập nhật" },
            { "Details", "Chi tiết" },
            { "Back to List", "Quay lại danh sách" },
            { "Search", "Tìm kiếm" },
            { "Clear", "Xóa bộ lọc" },
            { "View Details", "Xem chi tiết" },
            { "My Order History", "Lịch sử đơn hàng của tôi" },
            
            // Others
            { "Actions", "Thao tác" },
            { "Image", "Hình ảnh" },
            { "Name", "Tên" },
            { "Price", "Giá bán" },
            { "Description", "Mô tả" },
            { "Availability", "Tình trạng" },
            { "in stock", "trong kho" },
            { "Out of stock", "Hết hàng" },
            
            // FrontEnd specific
            { "Home", "Trang chủ" },
            { "Power Up Your Performance", "Tối Đa Hóa Hiệu Năng Của Bạn" },
            { "Explore the absolute best in computer hardware, laptops, and accessories.", "Khám phá những phần cứng, laptop và phụ kiện máy tính tuyệt vời nhất." },
            { "Shop Now", "Mua Ngay" },
            { "Featured Products", "Sản Phẩm Nổi Bật" },
            { "View all", "Xem tất cả" },
            { "Select a color", "Vui lòng chọn màu" },
            { "Add to Cart", "Thêm vào giỏ" },
            { "Customer Reviews", "Đánh giá từ khách hàng" },
            { "Write a Review", "Viết đánh giá" },
            { "Rating", "Điểm đánh giá" },
            { "Comment", "Bình luận" },
            { "Submit Review", "Gửi đánh giá" },
            { "No reviews yet. Be the first to review this product!", "Chưa có đánh giá nào. Hãy trở thành người đầu tiên đánh giá sản phẩm này!" },
            { "Excellent", "Tuyệt vời" },
            { "Good", "Tốt" },

            // Cart
            { "Shopping Cart", "Giỏ hàng" },
            { "Your Shopping Cart", "Giỏ hàng của bạn" },
            { "Your cart is currently empty.", "Giỏ hàng của bạn đang trống." },
            { "Looks like you haven't added anything to your cart yet.", "Có vẻ như bạn chưa thêm sản phẩm nào vào giỏ hàng." },
            { "Start Shopping", "Bắt đầu mua sắm" },
            { "Continue Shopping", "Tiếp tục mua sắm" },
            { "Proceed to Checkout", "Tiến hành thanh toán" },
            { "Please login to checkout", "Vui lòng đăng nhập để thanh toán" },
            { "Product", "Sản phẩm" },

            // Colors
            { "Yellow", "Vàng" },
            { "Silver", "Bạc" },
            { "Gray", "Xám" },
            { "Black", "Đen" },
            { "White", "Trắng" },
            { "Blue", "Xanh dương" },
            { "Red", "Đỏ" },
            { "Green", "Xanh lá" },
            { "Pink", "Hồng" },
            { "Gold", "Vàng Gold" },
            { "Midnight", "Xanh đen" },
            { "Starlight", "Trắng ánh sao" },
            { "Space Gray", "Xám không gian" },
            { "Space Grey", "Xám không gian" },

            // Auth & Account
            { "Login", "Đăng nhập" },
            { "Log in", "Đăng nhập" },
            { "Register", "Đăng ký" },
            { "Logout", "Đăng xuất" },
            { "Create a new account", "Tạo tài khoản mới" },
            { "Full Name", "Họ và tên" },
            { "Password", "Mật khẩu" },
            { "Confirm Password", "Xác nhận mật khẩu" },
            { "Already have an account? Login here", "Đã có tài khoản? Đăng nhập tại đây" },
            { "Remember me", "Ghi nhớ đăng nhập" },
            { "Forgot password?", "Quên mật khẩu?" },
            { "Don't have an account?", "Chưa có tài khoản?" },
            { "Register now", "Đăng ký ngay" },
            { "Welcome Back", "Chào mừng trở lại" },
            { "Login to your account", "Đăng nhập vào tài khoản của bạn" },
            { "Or continue with", "Hoặc tiếp tục với" },
            { "Admin Panel", "Bảng quản trị" },
            { "Staff Panel", "Bảng nhân viên" },
            { "Tài khoản", "Tài khoản" },
            { "Đơn mua", "Đơn mua" },
            { "My Account", "Tài khoản của tôi" },
            { "My Profile", "Hồ sơ của tôi" },
            { "Account Role", "Vai trò tài khoản" },
            { "Account Status", "Trạng thái tài khoản" },
            { "Active", "Đang hoạt động" },
            { "Locked", "Đã khóa" },
            { "View My Orders", "Xem đơn hàng của tôi" },
            { "Log out", "Đăng xuất" },
            { "Admin", "Quản trị viên" },
            { "System Administrator", "Quản trị viên hệ thống" },

            // Admin Product specific
            { "Delete Product", "Xóa sản phẩm" },
            { "Edit Product", "Chỉnh sửa sản phẩm" },
            { "Create Product", "Thêm sản phẩm mới" },
            { "Are you sure you want to delete this product?", "Bạn có chắc chắn muốn xóa sản phẩm này?" },
            { "This action cannot be undone and will remove the product and its image.", "Hành động này không thể hoàn tác và sẽ xóa bỏ sản phẩm cùng hình ảnh liên quan." },
            { "Permanently Delete", "Xóa vĩnh viễn" },
            { "Add Spec", "Thêm thông số" },
            { "Add Variant", "Thêm biến thể" },
            { "Current Image", "Hình ảnh hiện tại" },
            { "Upload Image", "Tải lên hình ảnh mới" },
            { "Are you sure you want to delete this review?", "Bạn có chắc chắn muốn xóa đánh giá này?" },
            { "Save Changes", "Lưu thay đổi" },
            { "Product Colors & Inventory", "Màu sắc & Tồn kho" },
            { "Removing a variant here will delete it from the database upon saving.", "Xóa biến thể tại đây sẽ xóa nó khỏi cơ sở dữ liệu khi bạn lưu." },
            { "Leave empty to keep the existing image.", "Để trống nếu muốn giữ nguyên hình ảnh cũ." },
            { "No Image Uploaded", "Chưa có hình ảnh" },
            { "Tên thông số", "Tên thông số" },
            { "Giá trị", "Giá trị" },
            { "Ví dụ: Tần số quét", "Ví dụ: Tần số quét" },
            { "Ví dụ: 144Hz", "Ví dụ: 144Hz" },
            { "HEX", "Mã màu (HEX)" },
            { "Name (Red, Blue...)", "Tên màu (Đỏ, Xanh...)" },
            { "Stock", "Tồn kho" },
            { "Built with ASP.NET Core MVC & Bootstrap 5", "Xây dựng bằng ASP.NET Core MVC & Bootstrap 5" }
        };

        public static string T(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return key;

            if (_currentLanguage == "vi-VN")
            {
                if (_vnTranslations.TryGetValue(key, out var translated))
                {
                    return translated;
                }
            }

            return key;
        }

        public static string FormatCurrency(decimal amount)
        {
            return amount.ToString("N0") + " VNĐ";
        }

        public static string FormatDate(DateTime date)
        {
            return date.ToString("dd/MM/yyyy HH:mm");
        }

        public const string Required = "Bắt buộc nhập";
        public const string Invalid = "Không hợp lệ";
    }
}
