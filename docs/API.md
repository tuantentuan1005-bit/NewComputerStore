# Tài liệu REST API — NewComputerStore (Giai đoạn 1, 2 & 3: Auth, Google Login, Catalog, Orders & Reviews)

## 1. Thông tin chung

- **Base URL (LAN)**: `http://<IP_MAY_BACKEND>:5000` (ví dụ: `http://192.168.1.57:5000`)
- **Swagger UI (Development)**: `http://<IP_MAY_BACKEND>:5000/swagger`
- **Content-Type**: `application/json; charset=utf-8`
- **Cơ chế xác thực**: `Authorization: Bearer <accessToken>` (JWT HS256, thời hạn mặc định 60 phút)
- **Hỗ trợ SignalR (`/chatHub`)**: Chấp nhận truyền token qua query string `?access_token=<accessToken>` hoặc header `Authorization: Bearer <accessToken>`.

---

## 2. Định dạng lỗi thống nhất (`ProblemDetails` - RFC 7807)

Mọi lỗi từ các route `/api/*` đều trả về JSON theo chuẩn `ProblemDetails` (không bao giờ redirect về trang HTML `/Account/Login`), kèm trường mở rộng `code` để phía Android xử lý theo mã lỗi:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Tài khoản chưa xác thực email",
  "status": 403,
  "detail": "Tài khoản chưa được xác thực. Vui lòng nhập mã OTP đã gửi đến email của bạn.",
  "instance": "/api/auth/login",
  "code": "EMAIL_NOT_VERIFIED",
  "email": "khachhang@gmail.com"
}
```

### Danh sách mã lỗi (`code`) phổ biến

| HTTP Status | `code` | Ý nghĩa |
|---|---|---|
| `400` | `OTP_INVALID` | Nhập sai mã OTP (thông báo số lần còn lại trong `detail`) |
| `400` | `OTP_EXPIRED` | Mã OTP đã quá hạn 5 phút |
| `400` | `OTP_LOCKED` | Nhập sai OTP quá 5 lần, mã hiện tại bị hủy |
| `400` | `OTP_INACTIVE` | Chưa có mã OTP hoặc mã đã bị hủy |
| `400` | `EMAIL_ALREADY_VERIFIED` | Tài khoản đã xác thực email trước đó |
| `400` | `INVALID_RESET_TOKEN` | Token đặt lại mật khẩu không hợp lệ hoặc đã quá hạn 15 phút |
| `400` | `EMPTY_ORDER` | Đơn hàng không có sản phẩm nào |
| `400` | `INVALID_QUANTITY` | Số lượng sản phẩm <= 0 |
| `400` | `PRODUCT_UNAVAILABLE` | Sản phẩm không tồn tại, đã ẩn hoặc ngừng kinh doanh |
| `400` | `COLOR_REQUIRED` | Sản phẩm có phân loại màu nhưng chưa truyền `colorId` |
| `400` | `INVALID_COLOR` | `colorId` không hợp lệ với sản phẩm |
| `400` | `INSUFFICIENT_STOCK` | Không đủ số lượng tồn kho (theo màu hoặc theo sản phẩm) |
| `400` | `INVALID_VOUCHER` | Mã giảm giá (`voucherCode`) không hợp lệ |
| `400` | `INVALID_PAYMENT_METHOD` | Gọi lấy QR cho đơn hàng không phải phương thức `"QR"` |
| `400` | `INVALID_RATING` | Điểm đánh giá (`rating`) không nằm trong khoảng 1..5 |
| `400` | `INVALID_COMMENT` | Nội dung đánh giá (`comment`) trống hoặc quá 1000 ký tự |
| `401` | `INVALID_CREDENTIALS` | Sai email hoặc mật khẩu |
| `401` | `INVALID_GOOGLE_TOKEN` | Google `idToken` không hợp lệ, sai `ClientId` hoặc đã hết hạn |
| `401` | `UNAUTHORIZED` | Thiếu JWT token, token sai chữ ký hoặc đã hết hạn |
| `403` | `EMAIL_NOT_VERIFIED` | Tài khoản Customer chưa xác thực OTP (hệ thống tự gửi lại OTP nếu mã cũ hết hạn) |
| `403` | `ACCOUNT_LOCKED` | Tài khoản đã bị khóa (`IsActive = false`) |
| `403` | `FORBIDDEN` | Truy cập đơn hàng hoặc tài nguyên của người dùng khác |
| `404` | `USER_NOT_FOUND` | Không tìm thấy người dùng |
| `404` | `PRODUCT_NOT_FOUND` | Không tìm thấy sản phẩm (hoặc sản phẩm đã ẩn/ngừng kinh doanh) |
| `404` | `ORDER_NOT_FOUND` | Không tìm thấy đơn hàng |
| `409` | `EMAIL_ALREADY_EXISTS` | Email đăng ký đã tồn tại |
| `409` | `CONCURRENCY_CONFLICT` | Xung đột tồn kho (`RowVersion`) khi nhiều người đặt cùng lúc |
| `429` | `OTP_THROTTLED` | Gửi lại OTP quá nhanh (< 60 giây), có kèm `"retryAfterSeconds"` |

---

## 3. Nhóm API Xác thực (`/api/auth` & `/api/me`)

### 3.1. Đăng ký tài khoản Customer
- **Endpoint**: `POST /api/auth/register`
- **Auth**: Public

**Request Body:**
```json
{
  "fullName": "Nguyễn Văn A",
  "email": "nguyenvana@gmail.com",
  "password": "Password123",
  "confirmPassword": "Password123"
}
```

**Response `200 OK`:**
```json
{
  "message": "Đăng ký thành công! Mã xác thực OTP đã được gửi đến email của bạn.",
  "email": "nguyenvana@gmail.com"
}
```

---

### 3.2. Xác thực mã OTP
- **Endpoint**: `POST /api/auth/verify-otp`
- **Auth**: Public

**Request Body:**
```json
{
  "email": "nguyenvana@gmail.com",
  "otp": "123456"
}
```

**Response `200 OK`:**
```json
{
  "message": "Xác thực tài khoản thành công.",
  "email": "nguyenvana@gmail.com"
}
```

---

### 3.3. Gửi lại mã OTP (cooldown 60 giây)
- **Endpoint**: `POST /api/auth/resend-otp`
- **Auth**: Public

**Request Body:**
```json
{
  "email": "nguyenvana@gmail.com"
}
```

**Response `200 OK`:**
```json
{
  "message": "Mã OTP mới đã được gửi thành công đến Gmail của bạn.",
  "email": "nguyenvana@gmail.com"
}
```

---

### 3.4. Đăng nhập Email/Password (Nhận JWT Access Token)
- **Endpoint**: `POST /api/auth/login`
- **Auth**: Public

**Request Body:**
```json
{
  "email": "nguyenvana@gmail.com",
  "password": "Password123"
}
```

**Response `200 OK`:**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2026-10-27T08:35:00Z",
  "user": {
    "id": 12,
    "fullName": "Nguyễn Văn A",
    "email": "nguyenvana@gmail.com",
    "role": "Customer"
  }
}
```

---

### 3.5. Đăng nhập bằng Google (`POST /api/auth/google`)
- **Endpoint**: `POST /api/auth/google`
- **Auth**: Public
- **Lưu ý cho Android**: Sử dụng Web Client ID (`780474588634-ngadk22vlb75quda0pndn7e5f9ntdke8.apps.googleusercontent.com`) trong `GetGoogleIdOption.Builder().setServerClientId(...)` (Credential Manager) hoặc `GoogleSignInOptions.Builder().requestIdToken(...)` để lấy `idToken` gửi lên server.
- Server xác thực chữ ký & `Audience` bằng `GoogleJsonWebSignature.ValidateAsync`, tự động tạo tài khoản `Customer` mới (kèm gửi email chào mừng) hoặc khôi phục/đăng nhập tài khoản đã có giống hệt luồng Web MVC rồi cấp JWT.

**Request Body:**
```json
{
  "idToken": "eyJhbGciOiJSUzI1NiIsImtpZCI6Ij..."
}
```

**Response `200 OK`:**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2026-10-27T09:20:00Z",
  "user": {
    "id": 15,
    "fullName": "Nguyễn Văn A",
    "email": "nguyenvana@gmail.com",
    "role": "Customer"
  }
}
```

**Response `401 Unauthorized` (khi `idToken` sai hoặc hết hạn):**
```json
{
  "title": "Đăng nhập Google thất bại",
  "status": 401,
  "detail": "Google idToken không hợp lệ hoặc đã hết hạn.",
  "instance": "/api/auth/google",
  "code": "INVALID_GOOGLE_TOKEN"
}
```

---

### 3.6. Quên mật khẩu & Đặt lại mật khẩu
- **Quên mật khẩu**: `POST /api/auth/forgot-password` — Body: `{ "email": "nguyenvana@gmail.com" }`
- **Đặt lại mật khẩu**: `POST /api/auth/reset-password` — Body: `{ "email": "nguyenvana@gmail.com", "token": "CfDJ8...", "newPassword": "NewPassword123", "confirmNewPassword": "NewPassword123" }`

---

### 3.7. Lấy thông tin người dùng hiện tại (`/api/me`)
- **Endpoint**: `GET /api/me`
- **Auth**: `Bearer <accessToken>` (Bắt buộc)

**Response `200 OK`:**
```json
{
  "id": 12,
  "fullName": "Nguyễn Văn A",
  "email": "nguyenvana@gmail.com",
  "role": "Customer"
}
```

---

## 4. Nhóm API Danh mục & Sản phẩm (Public)

### 4.1. Danh sách danh mục
- **Endpoint**: `GET /api/categories`
- **Auth**: Public

### 4.2. Danh sách sản phẩm (Phân trang, Tìm kiếm, Lọc, Sắp xếp)
- **Endpoint**: `GET /api/products?categoryId=&q=&page=1&pageSize=20&sort=`
- **Auth**: Public
- **Tham số `sort`**: `newest` (mặc định), `price_asc`, `price_desc`, `name_asc`, `name_desc`.

### 4.3. Chi tiết sản phẩm
- **Endpoint**: `GET /api/products/{id}`
- **Auth**: Public

---

## 5. Nhóm API Đặt hàng & Đơn mua (`/api/orders`) — Yêu cầu JWT Token

### 5.1. Tạo đơn hàng mới (`POST /api/orders`)
- **Endpoint**: `POST /api/orders`
- **Auth**: `Bearer <accessToken>` (Bắt buộc)

**Request Body:**
```json
{
  "items": [
    {
      "productId": 6,
      "colorId": 8,
      "quantity": 1
    }
  ],
  "fullName": "Nguyễn Văn A",
  "phone": "0901234567",
  "address": "123 Nguyễn Huệ, Quận 1, TP.HCM",
  "email": "nguyenvana@gmail.com",
  "paymentMethod": "QR",
  "voucherCode": null,
  "note": "Giao giờ hành chính"
}
```

### 5.2. Danh sách đơn hàng của tôi (`GET /api/orders`)
- **Endpoint**: `GET /api/orders`
- **Auth**: `Bearer <accessToken>` (Bắt buộc)

### 5.3. Chi tiết đơn hàng (`GET /api/orders/{id}`)
- **Endpoint**: `GET /api/orders/{id}`
- **Auth**: `Bearer <accessToken>` (Bắt buộc — chỉ chủ đơn hàng mới được xem)

### 5.4. Lấy thông tin QR thanh toán của đơn hàng (`GET /api/orders/{id}/payment-qr`)
- **Endpoint**: `GET /api/orders/{id}/payment-qr`
- **Auth**: `Bearer <accessToken>` (Bắt buộc — chỉ chủ đơn hàng mới được xem, và đơn hàng phải có `paymentMethod = "QR"`)

---

## 6. Nhóm API Đánh giá sản phẩm (`Reviews`)

### 6.1. Lấy danh sách đánh giá của một sản phẩm (`GET /api/products/{id}/reviews`)
- **Endpoint**: `GET /api/products/{id}/reviews`
- **Auth**: Public

**Response `200 OK`:**
```json
{
  "productId": 6,
  "totalReviews": 2,
  "averageRating": 4.5,
  "items": [
    {
      "reviewId": 10,
      "productId": 6,
      "userId": 1,
      "userFullName": "System Administrator",
      "rating": 5,
      "comment": "Máy chạy rất mượt, màn hình đẹp!"
    },
    {
      "reviewId": 9,
      "productId": 6,
      "userId": 12,
      "userFullName": "Nguyễn Văn A",
      "rating": 4,
      "comment": "Giao hàng nhanh, đóng gói kỹ."
    }
  ]
}
```

**Response `404 Not Found` (nếu sản phẩm không tồn tại hoặc đã ẩn/xóa):**
```json
{
  "title": "Không tìm thấy sản phẩm",
  "status": 404,
  "detail": "Không tìm thấy sản phẩm với mã #999 hoặc sản phẩm đã ngừng kinh doanh.",
  "instance": "/api/products/999/reviews",
  "code": "PRODUCT_NOT_FOUND"
}
```

---

### 6.2. Gửi đánh giá sản phẩm mới (`POST /api/reviews`)
- **Endpoint**: `POST /api/reviews`
- **Auth**: `Bearer <accessToken>` (Bắt buộc — chỉ người dùng đã đăng nhập mới được đánh giá)
- **Quy tắc kiểm tra**:
  - `rating` bắt buộc là số nguyên từ `1` đến `5`.
  - `comment` bắt buộc không được để trống, tối đa `1000` ký tự.
  - `productId` phải là sản phẩm đang kinh doanh (`!IsDeleted && !IsHidden`).

**Request Body:**
```json
{
  "productId": 6,
  "rating": 5,
  "comment": "Máy chạy rất mượt, màn hình đẹp!"
}
```

**Response `201 Created`:**
```json
{
  "reviewId": 10,
  "productId": 6,
  "userId": 1,
  "userFullName": "System Administrator",
  "rating": 5,
  "comment": "Máy chạy rất mượt, màn hình đẹp!"
}
```

---

## 7. Nhóm API Chat & Hỗ trợ trực tuyến (`Chat` + `SignalR /chatHub`)

Hệ thống Chat dùng chung một `ChatService` và `ChatHub` (`/chatHub`) cho cả Web (Cookie) lẫn Android (JWT Bearer). Khi khách hàng trên Android nhắn tin, nhân viên tại trang Quản trị Web (`/Chat/StaffDashboard`) sẽ nhận tin nhắn theo thời gian thực và ngược lại.

### 7.1. Lấy lịch sử chat & thông tin phiên hội thoại (`GET /api/chat/history`)
- **Endpoint**: `GET /api/chat/history?orderId=&skip=0&take=50`
- **Auth**: `Bearer <accessToken>` (Bắt buộc)
- **Mô tả**:
  - Tự động lấy phiên hội thoại đang mở của người dùng hiện tại (hoặc tạo mới nếu chưa có).
  - Trả về danh sách tin nhắn theo thứ tự thời gian tăng dần (cũ → mới) và tự động đánh dấu đã đọc các tin nhắn từ nhân viên.
- **Query Parameters**:
  - `orderId` (tùy chọn): Gắn hội thoại với một đơn hàng cụ thể.
  - `skip` (mặc định `0`): Số tin nhắn bỏ qua.
  - `take` (mặc định `50`, tối đa `200`): Số tin nhắn cần lấy.

**Response `200 OK`:**
```json
{
  "conversationId": 3,
  "customerId": 1,
  "orderId": null,
  "status": "Waiting",
  "staffId": null,
  "staffName": "Nhân viên hỗ trợ",
  "createdAt": "2026-10-27T15:30:00",
  "lastMessageAt": "2026-10-27T15:35:10",
  "messages": [
    {
      "id": 15,
      "conversationId": 3,
      "senderId": 1,
      "senderName": "System Administrator",
      "content": "Shop tư vấn giúp mình mẫu Laptop HP 240R G10 nhé!",
      "type": "Text",
      "metadata": null,
      "sentAt": "15:35",
      "sentAtFull": "2026-10-27T15:35:10",
      "isRead": false,
      "isStaff": false
    }
  ]
}
```

---

### 7.2. Gửi tin nhắn qua REST API (`POST /api/chat/messages`) *(Tùy chọn thay cho SignalR invoke)*
- **Endpoint**: `POST /api/chat/messages`
- **Auth**: `Bearer <accessToken>` (Bắt buộc)
- **Mô tả**: Lưu tin nhắn vào DB qua `IChatService` và phát sự kiện SignalR `ReceiveMessage` + `GlobalNewMessage` tới tất cả thiết bị đang kết nối trong cuộc hội thoại.

**Request Body:**
```json
{
  "conversationId": 3,
  "content": "Shop tư vấn giúp mình mẫu Laptop HP 240R G10 nhé!"
}
```

**Response `200 OK`:**
```json
{
  "id": 15,
  "conversationId": 3,
  "senderId": 1,
  "senderName": "System Administrator",
  "content": "Shop tư vấn giúp mình mẫu Laptop HP 240R G10 nhé!",
  "type": "Text",
  "metadata": null,
  "sentAt": "15:35",
  "sentAtFull": "2026-10-27T15:35:10",
  "isRead": false,
  "isStaff": false
}
```

---

### 7.3. Đánh dấu đã đọc (`POST /api/chat/read`)
- **Endpoint**: `POST /api/chat/read`
- **Auth**: `Bearer <accessToken>`

**Request Body:**
```json
{
  "conversationId": 3
}
```

---

### 7.4. Hướng dẫn kết nối SignalR (`/chatHub`) trên Android (Kotlin)

#### Bước 1: Thêm thư viện SignalR Client vào `build.gradle.kts` (Module `:app`)
```kotlin
dependencies {
    implementation("com.microsoft.signalr:signalr:8.0.0")
    implementation("io.reactivex.rxjava3:rxjava:3.1.8")
}
```

#### Bước 2: Quy trình chuẩn khi mở màn hình Chat trên Android
1. Gọi `GET /api/chat/history` (với Header `Authorization: Bearer <accessToken>`) để:
   - Lấy `conversationId` hiện tại của khách hàng.
   - Hiển thị danh sách tin nhắn lịch sử (`messages`).
2. Khởi tạo `HubConnection` tới `http://<IP_LAN>:5000/chatHub` kèm JWT Token:
   - Hub `/chatHub` hỗ trợ nhận JWT qua cả Header `Authorization: Bearer <token>` (do `withAccessTokenProvider` của SignalR SDK tự gắn) và query string `?access_token=<token>`.
3. Khi kết nối thành công:
   - Gọi `hubConnection.invoke("JoinConversation", conversationId)` (hoặc hệ thống cũng tự động join các cuộc hội thoại đang mở của user ngay trong `OnConnectedAsync`).

#### Bước 3: Các sự kiện (Server → Client) và Phương thức (Client → Server) trên `/chatHub`

| Chiều | Tên sự kiện / Method | Tham số | Mô tả |
|---|---|---|---|
| **Server → Client** | `ReceiveMessage` | `msg` (JSON object) | Nhận tin nhắn mới theo thời gian thực (`id`, `conversationId`, `senderId`, `senderName`, `content`, `type`, `metadata`, `sentAt`, `isStaff`). |
| **Server → Client** | `UserTyping` | `(conversationId: Int, userName: String)` | Hiển thị trạng thái `"Nhân viên đang nhập..."`. |
| **Server → Client** | `ConversationStatusChanged` | `(conversationId: Int, status: String)` | Trạng thái hội thoại thay đổi (`Open`, `Waiting`, `Answered`, `Closed`). |
| **Client → Server** | `JoinConversation` | `(conversationId: Int)` | Tham gia nhóm nhận tin nhắn của cuộc hội thoại (`Conversation_{id}`). Nếu truyền `0`, server tự lấy/tạo hội thoại của user. |
| **Client → Server** | `SendMessage` | `(conversationId: Int, content: String)` | Gửi tin nhắn trực tiếp qua SignalR (lưu vào DB qua `ChatService` và broadcast `ReceiveMessage` cho cả Web & Android). |
| **Client → Server** | `SendTyping` | `(conversationId: Int, userName: String)` | Báo cho đối phương biết người dùng đang gõ phím. |
| **Client → Server** | `MarkAsRead` | `(conversationId: Int)` | Đánh dấu đã đọc các tin nhắn trong cuộc hội thoại. |
| **Client → Server** | `LeaveConversation` | `(conversationId: Int)` | Rời nhóm hội thoại khi đóng màn hình. |

> **Lưu ý về tin nhắn loại `ProductCard`:**
> Khi nhân viên gửi thẻ sản phẩm từ trang quản trị Web, trường `type` sẽ là `"ProductCard"` và `metadata` là chuỗi JSON dạng:
> `{"id": 6, "name": "Laptop HP 240R G10...", "price": "15,990,000₫", "image": "ten_file_anh.jpg"}`
> Đường dẫn ảnh đầy đủ để hiển thị bằng Coil/Glide trên Android là: `http://<IP_LAN>:5000/images/{image}`.

#### Bước 4: Code mẫu Kotlin (`ChatSignalRManager.kt`)
```kotlin
import com.microsoft.signalr.HubConnection
import com.microsoft.signalr.HubConnectionBuilder
import com.microsoft.signalr.HubConnectionState
import io.reactivex.rxjava3.core.Single

data class IncomingChatMessage(
    val id: Int = 0,
    val conversationId: Int = 0,
    val senderId: Int = 0,
    val senderName: String = "",
    val content: String = "",
    val type: String = "Text",
    val metadata: String? = null,
    val sentAt: String = "",
    val isStaff: Boolean = false
)

class ChatSignalRManager(
    private val baseUrl: String, // Ví dụ: "http://192.168.1.57:5000"
    private val tokenProvider: () -> String
) {
    private var hubConnection: HubConnection? = null

    fun connect(
        conversationId: Int,
        onMessageReceived: (IncomingChatMessage) -> Unit,
        onUserTyping: (Int, String) -> Unit
    ) {
        hubConnection = HubConnectionBuilder.create("$baseUrl/chatHub")
            .withAccessTokenProvider(Single.defer { Single.just(tokenProvider()) })
            .build()

        // Lắng nghe tin nhắn mới từ Server
        hubConnection?.on(
            "ReceiveMessage",
            { msg -> onMessageReceived(msg) },
            IncomingChatMessage::class.java
        )

        // Lắng nghe sự kiện đang gõ phím
        hubConnection?.on(
            "UserTyping",
            { convId, userName -> onUserTyping(convId, userName) },
            Int::class.javaObjectType,
            String::class.java
        )

        hubConnection?.start()?.doOnComplete {
            // Tham gia phòng chat của cuộc hội thoại
            hubConnection?.invoke("JoinConversation", conversationId)
        }?.subscribe(
            {},
            { error -> error.printStackTrace() }
        )
    }

    fun sendMessage(conversationId: Int, content: String) {
        if (hubConnection?.connectionState == HubConnectionState.CONNECTED) {
            hubConnection?.invoke("SendMessage", conversationId, content)
        }
    }

    fun sendTyping(conversationId: Int, userName: String) {
        if (hubConnection?.connectionState == HubConnectionState.CONNECTED) {
            hubConnection?.invoke("SendTyping", conversationId, userName)
        }
    }

    fun disconnect(conversationId: Int) {
        if (hubConnection?.connectionState == HubConnectionState.CONNECTED) {
            hubConnection?.invoke("LeaveConversation", conversationId)
            hubConnection?.stop()
        }
    }
}
```
