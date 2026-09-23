# Tham chiếu API

Mọi endpoint đi qua gateway tại `http://localhost:5000`. Gọi thẳng service cũng được
(cổng 5100–5105) nhưng khi đó phải tự xử lý CORS.

## Định dạng phản hồi

Mọi endpoint (trừ `204 No Content`) trả về envelope thống nhất:

```json
{
  "success": true,
  "data": { },
  "error": null,
  "correlationId": "a1b2c3...",
  "timestamp": "2026-09-23T10:00:00Z"
}
```

Khi lỗi:

```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "product.sku_duplicated",
    "message": "SKU 'LAP-001' đã tồn tại.",
    "details": null
  },
  "correlationId": "a1b2c3...",
  "timestamp": "2026-09-23T10:00:00Z"
}
```

| HTTP | Khi nào |
|---|---|
| 400 | Dữ liệu không hợp lệ (`details` liệt kê từng lỗi field) |
| 401 | Thiếu/hết hạn token |
| 403 | Không đủ quyền, hoặc truy cập tài nguyên của người khác |
| 404 | Không tìm thấy |
| 409 | Xung đột (SKU trùng, hết hàng, concurrency) |
| 429 | Vượt rate limit |
| 500 | Lỗi hệ thống |

## Xác thực

```
Authorization: Bearer <access_token>
```

Access token sống 60 phút. Hết hạn thì gọi `POST /api/auth/refresh` với refresh token
(frontend đã tự động hóa việc này).

---

## Identity — `/api/auth`, `/api/users`

| Method | Endpoint | Quyền | Mô tả |
|---|---|---|---|
| POST | `/api/auth/register` | — | Đăng ký (mặc định vai trò `Customer`) |
| POST | `/api/auth/login` | — | Đăng nhập, trả access + refresh token |
| POST | `/api/auth/refresh` | — | Xoay vòng token |
| POST | `/api/auth/logout` | Đăng nhập | Thu hồi mọi refresh token |
| GET | `/api/users/me` | Đăng nhập | Hồ sơ hiện tại |
| PUT | `/api/users/me` | Đăng nhập | Cập nhật họ tên, điện thoại |
| POST | `/api/users/me/change-password` | Đăng nhập | Đổi mật khẩu (thu hồi mọi phiên) |
| GET | `/api/users` | Admin | Danh sách, có `search`, `role`, `isActive`, phân trang |
| PUT | `/api/users/{id}/roles` | Admin | Gán vai trò |
| PATCH | `/api/users/{id}/status` | Admin | Khóa / mở khóa |

```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"admin@shop.local","password":"Admin@123"}'
```

---

## Product — `/api/products`, `/api/categories`

| Method | Endpoint | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/products` | — | Tìm kiếm. Query: `search`, `categoryId`, `isActive`, `minPrice`, `maxPrice`, `sortBy` (`createdAt`\|`name`\|`price`\|`sku`), `descending`, `page`, `pageSize` |
| GET | `/api/products/{id}` | — | Chi tiết (có cache Redis 10 phút) |
| POST | `/api/products` | Admin, Manager | Tạo mới — phát `product.created`, Inventory tự tạo bản ghi tồn kho |
| PUT | `/api/products/{id}` | Admin, Manager | Sửa tên/mô tả/danh mục/ảnh |
| PATCH | `/api/products/{id}/price` | Admin, Manager | Đổi giá — phát `product.price_changed` |
| PATCH | `/api/products/{id}/status` | Admin, Manager | Bật/tắt bán |
| DELETE | `/api/products/{id}` | Admin | Xóa — phát `product.deleted` |
| GET | `/api/categories` | — | Danh sách (`onlyActive=true` để lọc) |
| GET | `/api/categories/{id}` | — | Chi tiết |
| POST | `/api/categories` | Admin, Manager | Tạo |
| PUT | `/api/categories/{id}` | Admin, Manager | Sửa |
| DELETE | `/api/categories/{id}` | Admin | Xóa — **409** nếu còn sản phẩm bên trong |

```bash
curl -X POST http://localhost:5000/api/products \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{
    "sku": "TEST-001",
    "name": "Sản phẩm thử",
    "price": 250000,
    "currency": "VND",
    "categoryId": "<guid>",
    "initialStock": 50
  }'
```

---

## Inventory — `/api/stocks`, `/api/reservations`

| Method | Endpoint | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/stocks` | — | Query: `search`, `lowStockOnly`, `warehouseCode`, phân trang |
| GET | `/api/stocks/low-stock` | — | Mặt hàng dưới ngưỡng cảnh báo |
| GET | `/api/stocks/product/{productId}` | — | Tồn kho 1 sản phẩm |
| GET | `/api/stocks/product/{productId}/movements` | — | Nhật ký biến động |
| POST | `/api/stocks` | Admin, Manager | Tạo bản ghi tồn kho thủ công |
| POST | `/api/stocks/product/{productId}/receive` | Admin, Manager | Nhập kho |
| POST | `/api/stocks/product/{productId}/adjust` | Admin, Manager | Điều chỉnh sau kiểm kê |
| PATCH | `/api/stocks/product/{productId}/reorder-level` | Admin, Manager | Đặt ngưỡng cảnh báo |
| GET | `/api/reservations/order/{orderId}` | — | Xem giữ chỗ của 1 đơn |
| POST | `/api/reservations` | Đăng nhập | Giữ chỗ thủ công (luồng chính dùng gRPC) |
| POST | `/api/reservations/order/{orderId}/commit` | Admin, Manager | Chốt giữ chỗ |
| POST | `/api/reservations/order/{orderId}/release` | Admin, Manager | Nhả giữ chỗ |

> `quantityAvailable = quantityOnHand − quantityReserved`

---

## Order — `/api/orders`

Tất cả endpoint yêu cầu đăng nhập. Khách chỉ thấy đơn của mình; Admin/Manager thấy tất cả.

| Method | Endpoint | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/orders` | Đăng nhập | Query: `status`, `orderNumber`, `fromUtc`, `toUtc`, phân trang |
| GET | `/api/orders/statistics` | Đăng nhập | Số đơn theo trạng thái + doanh thu |
| GET | `/api/orders/{id}` | Chủ đơn / Staff | Chi tiết đầy đủ |
| POST | `/api/orders` | Đăng nhập | **Khởi động saga đặt hàng** |
| POST | `/api/orders/{id}/cancel` | Chủ đơn / Staff | Hủy — phát `order.cancelled`, kho tự nhả |
| POST | `/api/orders/{id}/ship` | Admin, Manager | Chuyển sang đang giao |
| POST | `/api/orders/{id}/complete` | Admin, Manager | Hoàn tất |

```bash
curl -X POST http://localhost:5000/api/orders \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{
    "lines": [ { "productId": "<guid>", "quantity": 2 } ],
    "shippingAddress": {
      "recipientName": "Nguyễn Văn A",
      "phone": "0901234567",
      "street": "12 Nguyễn Huệ",
      "ward": "Bến Nghé",
      "district": "Quận 1",
      "city": "Hồ Chí Minh"
    },
    "paymentMethod": "CreditCard"
  }'
```

**Lưu ý**: không gửi giá lên — server tự lấy giá từ catalog qua gRPC.

### Vòng đời trạng thái

```
Pending → AwaitingPayment → Confirmed → Shipped → Completed
                 │               │
                 └───────────────┴──────▶ Cancelled
```

---

## Payment — `/api/payments`

| Method | Endpoint | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/payments` | Đăng nhập | Query: `orderId`, `customerId`, `status`, `method`, phân trang |
| GET | `/api/payments/{id}` | Đăng nhập | Chi tiết + lịch sử hoàn tiền |
| GET | `/api/payments/order/{orderId}` | Đăng nhập | Giao dịch của 1 đơn |
| POST | `/api/payments` | Đăng nhập | Tạo thủ công (luồng chính là tự động qua Kafka) |
| POST | `/api/payments/{id}/capture` | Admin, Manager | Thu tiền giao dịch đang `Authorized` (COD) |
| POST | `/api/payments/{id}/refund` | Admin, Manager | Hoàn tiền (hỗ trợ hoàn một phần) |
| POST | `/api/payments/{id}/fail` | Admin | Đánh dấu thất bại thủ công |

**Cổng thanh toán giả lập**: đơn có tổng tiền chia hết cho 13 luôn bị từ chối
(`PaymentGateway__ForceFailAmountMultiple`), cộng thêm 10% lỗi ngẫu nhiên.

---

## Notification — `/api/notifications` + SignalR

| Method | Endpoint | Quyền | Mô tả |
|---|---|---|---|
| GET | `/api/notifications` | Đăng nhập | Thông báo của tôi + broadcast. Query: `isRead`, `severity`, phân trang |
| GET | `/api/notifications/unread-count` | Đăng nhập | Số chưa đọc |
| POST | `/api/notifications/{id}/read` | Đăng nhập | Đánh dấu đã đọc |
| POST | `/api/notifications/read-all` | Đăng nhập | Đánh dấu tất cả |
| DELETE | `/api/notifications/{id}` | Chủ sở hữu | Xóa |
| POST | `/api/notifications` | Admin, Manager | Gửi thủ công (có thể nhắm theo vai trò) |

### SignalR Hub

```
ws://localhost:5000/hubs/notifications?access_token=<jwt>
```

| Sự kiện từ server | Payload |
|---|---|
| `ReceiveNotification` | Object `AppNotification` đầy đủ |
| `UnreadCountChanged` | `number` |

| Method gọi lên server | Trả về |
|---|---|
| `Ping` | `"pong"` |

```typescript
const connection = new HubConnectionBuilder()
  .withUrl('/hubs/notifications', { accessTokenFactory: () => token })
  .withAutomaticReconnect()
  .build()

connection.on('ReceiveNotification', (n) => console.log(n.title))
await connection.start()
```

---

## gRPC

Không expose qua gateway — chỉ dùng nội bộ giữa các service. Reflection được bật ở môi trường
Development nên gọi thử được bằng `grpcurl`:

```bash
grpcurl -plaintext localhost:5101 list
grpcurl -plaintext -d '{"id":"<guid>"}' \
  localhost:5101 ecommerce.product.ProductGrpcService/GetProduct
```

| Service | Cổng | Method |
|---|---|---|
| ProductGrpcService | 5101 | `GetProduct`, `GetProductsByIds`, `ValidateProducts` |
| InventoryGrpcService | 5102 | `GetStock`, `GetStockBatch`, `ReserveStock`, `CommitReservation`, `ReleaseReservation` |
| OrderGrpcService | 5103 | `GetOrder`, `GetOrdersByCustomer` |
| PaymentGrpcService | 5104 | `AuthorizePayment`, `CapturePayment`, `RefundPayment`, `GetPaymentByOrder` |
| NotificationGrpcService | 5105 | `PushToUser`, `Broadcast`, `GetUnreadCount` |

> Số tiền trong proto truyền dạng **chuỗi** (`"129000.00"`) để tránh sai số dấu phẩy động.

---

## Health check

| Endpoint | Mục đích |
|---|---|
| `/health/live` | Tiến trình còn sống (dùng cho liveness probe) |
| `/health/ready` | Postgres + Redis + Kafka đều sẵn sàng (readiness probe) |
| `http://localhost:5000/health/services` | Gateway gom trạng thái toàn bộ service |
