# Kiến trúc chi tiết

Tài liệu này giải thích **vì sao** mỗi thứ được làm như vậy, không chỉ *nó là gì*.

---

## 1. Clean Architecture: chiều phụ thuộc

```
        ┌─────────────────────────────────────────┐
        │              Product.Api                │  Controllers, gRPC endpoint, Program.cs
        │   ┌─────────────────────────────────┐   │
        │   │      Product.Infrastructure     │   │  EF Core, Kafka mapper, repository impl
        │   │   ┌─────────────────────────┐   │   │
        │   │   │   Product.Application   │   │   │  Command/Query handler, validator
        │   │   │   ┌─────────────────┐   │   │   │
        │   │   │   │ Product.Domain  │   │   │   │  Entity, business rule
        │   │   │   └─────────────────┘   │   │   │
        │   │   └─────────────────────────┘   │   │
        │   └─────────────────────────────────┘   │
        └─────────────────────────────────────────┘
                   mũi tên phụ thuộc ───▶ hướng vào trong
```

### Quy tắc kiểm chứng được

Mở `Product.Domain.csproj` — nó chỉ tham chiếu `BuildingBlocks.Core`. Không EF Core, không ASP.NET,
không Kafka. Nghĩa là bạn có thể unit test toàn bộ business rule mà **không cần database**.

```csharp
// Test này chạy trong vài mili giây, không cần container nào
var product = ProductItem.Create("SKU-1", "Áo", null, 100_000m, "VND", categoryId, null);
product.ChangePrice(120_000m, "VND");
Assert.Single(product.DomainEvents.OfType<ProductPriceChangedDomainEvent>());
```

### Tại sao Repository interface nằm ở Domain?

Đây là **Dependency Inversion**. Domain định nghĩa *nó cần gì* (`IProductRepository`),
Infrastructure quyết định *làm thế nào* (EF Core + PostgreSQL). Đảo ngược lại thì Domain sẽ
phụ thuộc EF Core và mất tính độc lập.

---

## 2. CQRS tự viết thay vì MediatR

`BuildingBlocks.Application/Cqrs/Dispatcher.cs` — khoảng 60 dòng.

**Vì sao không dùng MediatR?**
1. Để bạn **nhìn thấy** cơ chế: dispatcher chỉ là resolve handler từ DI + gọi nó.
2. Không phụ thuộc license của thư viện bên thứ ba.
3. Pipeline behavior (validation, logging) nằm ngay trong wrapper, dễ đọc hơn chuỗi decorator.

**Cách hoạt động:**

```csharp
// 1. Controller gọi
await Dispatcher.Send(new CreateProductCommand(...), ct);

// 2. Dispatcher tra bảng cache, tạo wrapper generic đúng kiểu (chỉ reflect 1 lần/loại message)
var wrapper = Wrappers.GetOrAdd(command.GetType(), ...);

// 3. Wrapper: chạy validator → resolve handler từ DI → log thời gian → trả Result<T>
```

Chi phí reflection chỉ phát sinh lần đầu cho mỗi loại command; những lần sau lấy từ
`ConcurrentDictionary`.

### Result<T> thay vì exception

Lỗi nghiệp vụ ("SKU trùng", "hết hàng") là **kết quả hợp lệ**, không phải sự cố. Dùng exception
cho chúng vừa chậm vừa làm control flow khó đọc.

```csharp
if (await products.SkuExistsAsync(command.Sku, ct: ct))
    return Result.Failure<ProductDto>(ProductErrors.SkuDuplicated(command.Sku));
```

`ApiControllerBase.ToResponse()` quy đổi `ErrorType` sang HTTP status:
`NotFound → 404`, `Validation → 400`, `Conflict → 409`, `Forbidden → 403`.

Exception vẫn được dùng cho lỗi *thật sự bất thường* (mất kết nối DB) — `ExceptionHandlingMiddleware`
bắt và trả 500 mà không lộ stack trace.

---

## 3. Vì sao Inventory tách khỏi Product?

Đây là quyết định khó nhất khi chia service. Lý do:

| Tiêu chí | Catalog (Product) | Tồn kho (Inventory) |
|---|---|---|
| Tỉ lệ đọc/ghi | ~1000 : 1 | ~5 : 1 |
| Tranh chấp | Gần như không | **Cao** — nhiều đơn tranh 1 SKU |
| Cache | TTL 10 phút thoải mái | TTL 1 phút, phải invalidate ngay |
| Scale | Scale đọc (replica) | Scale ghi (sharding theo kho) |
| Dữ liệu thay đổi | Khi admin sửa | **Mỗi lần có đơn hàng** |

Gộp chung thì mỗi lần đặt hàng phải khóa bản ghi sản phẩm — chặn luôn cả việc đọc catalog.
Tách ra thì catalog vẫn phục vụ hàng nghìn request/giây trong khi tồn kho đang bị khóa.

**Cái giá phải trả**: dữ liệu giữa 2 service *eventually consistent*. Tạo sản phẩm mới xong,
bản ghi tồn kho xuất hiện sau vài trăm mili giây (khi `ProductCreatedIntegrationEvent` được
consume). Với bài toán này thì chấp nhận được.

---

## 4. Saga: orchestration hay choreography?

Dự án dùng **kết hợp**, không thuần một kiểu:

| Bước | Kiểu | Lý do |
|---|---|---|
| Định giá, giữ kho | **Orchestration** (Order gọi gRPC) | Cần kết quả ngay để trả lời khách "đặt được hay không" |
| Thanh toán | **Choreography** (event) | Có thể chậm vài giây; Order không nên chờ |
| Bồi hoàn | **Choreography** (event) | Phải chạy được cả khi Inventory đang chết |

### Vì sao bồi hoàn dùng event chứ không gọi gRPC?

Xem `CancelOrderHandler`:

```csharp
// KHÔNG gọi inventory.ReleaseAsync() ở đây
order.Cancel(command.Reason);
await unitOfWork.SaveChangesAsync(ct);   // phát OrderCancelled qua outbox
```

Nếu gọi gRPC trực tiếp, Inventory chết → hủy đơn cũng thất bại → khách bị kẹt. Với event,
đơn hủy ngay lập tức, còn việc nhả kho sẽ xảy ra khi Inventory sống lại. **Ba lớp lưới an toàn**:

1. Event `OrderCancelled` (chính),
2. gRPC `ReleaseAsync` khi lưu đơn thất bại giữa chừng (`PlaceOrderHandler`),
3. `ReservationExpiryJob` — sau 15 phút tự nhả dù không có tín hiệu nào.

### Thứ tự các bước không phải ngẫu nhiên

```
① Định giá (Catalog)  →  ② Giữ kho (Inventory)  →  ③ Ghi đơn  →  ④ Thanh toán
```

Nếu ② đứng trước ①: giữ kho xong mới phát hiện sản phẩm không tồn tại → phải bồi hoàn vô ích.
Nếu ③ đứng trước ②: đơn đã tồn tại mà kho không đủ → phải hủy đơn vừa tạo.

Đặt bước "có thể thất bại mà không để lại dấu vết" lên trước, bước "tạo ra thứ cần bồi hoàn" xuống sau.

---

## 5. Transactional Outbox — chi tiết

### Vấn đề

```csharp
await db.SaveChangesAsync();        // ✅ đơn hàng đã lưu
await kafka.PublishAsync(evt);      // ❌ service chết ngay đây → event mất vĩnh viễn
```

Đảo thứ tự cũng không cứu được: publish xong mà SaveChanges lỗi thì event nói về một đơn hàng
không tồn tại.

### Cách giải

`OutboxDomainEventInterceptor` móc vào `SavingChangesAsync` — tức là **trước** khi EF Core commit:

```csharp
public override ValueTask<InterceptionResult<int>> SavingChangesAsync(...)
{
    // Gom domain event của mọi aggregate đang track, map sang integration event,
    // thêm vào context.Set<OutboxMessage>() — nằm trong CÙNG transaction.
}
```

Kết quả: hoặc cả đơn hàng lẫn outbox message cùng được lưu, hoặc cả hai cùng rollback.
Không có trạng thái nửa vời.

`OutboxProcessor<TContext>` là `BackgroundService` chạy mỗi 5 giây, lấy 50 message chưa xử lý,
publish lên Kafka, đánh dấu `ProcessedOnUtc`. Publish lỗi thì tăng `RetryCount`; quá 5 lần thì dừng
và ghi `Error` để người vận hành xử lý.

### Đánh đổi

Độ trễ thêm tối đa 5 giây (chu kỳ polling). Có thể giảm xuống 1 giây hoặc dùng Postgres
`LISTEN/NOTIFY` để đẩy tức thì — nhưng với bài toán này 5 giây là chấp nhận được và đơn giản hơn nhiều.

---

## 6. Giữ chỗ kho: all-or-nothing + distributed lock

`ReserveStockHandler` là đoạn code tinh tế nhất dự án. Ba vấn đề cần giải cùng lúc:

### (a) Tránh deadlock

Đơn A giữ {sp1, sp2}, đơn B giữ {sp2, sp1}. Nếu khóa theo thứ tự đến thì A giữ sp1 chờ sp2,
B giữ sp2 chờ sp1 → deadlock.

```csharp
var orderedIds = command.Lines.Select(l => l.ProductId).Distinct()
    .OrderBy(id => id).ToList();   // ⬅ luôn khóa theo cùng một thứ tự
```

### (b) All-or-nothing

Kiểm tra đủ hàng cho **tất cả** dòng trước khi động vào bất kỳ dòng nào. Nếu kiểm tra và trừ
xen kẽ, một đơn có thể "giữ được một nửa" rồi thất bại, để lại tồn kho sai lệch.

### (c) Idempotent

Order service retry (do timeout mạng chẳng hạn) không được tạo 2 reservation:

```csharp
var existing = await reservations.GetByOrderIdAsync(command.OrderId, ct);
if (existing is { Status: Pending or Committed })
    return Result.Success(...);   // trả lại kết quả cũ
```

Cộng thêm unique index trên `reservations.order_id` làm chốt chặn ở tầng DB.

---

## 7. Redis: ba vai trò khác nhau

| Vai trò | Dùng ở đâu | Ghi chú |
|---|---|---|
| **Cache-aside** | Product (TTL 10ph), Inventory (TTL 1ph) | Lỗi Redis **không** làm gãy request — chỉ log rồi đi thẳng xuống DB |
| **Distributed lock** | Inventory khi giữ chỗ / nhập kho | `SET NX PX` + Lua script giải phóng |
| **SignalR backplane** | Notification | Cho phép scale nhiều instance notification-service |

### Vì sao cache lỗi không được ném exception?

```csharp
catch (Exception ex)
{
    logger.LogWarning(ex, "Đọc cache {Key} lỗi — fallback xuống nguồn dữ liệu", key);
    return default;
}
```

Cache là **tối ưu hóa**, không phải nguồn sự thật. Redis chết mà cả hệ thống chết theo thì
việc thêm cache đã làm giảm độ tin cậy thay vì tăng hiệu năng.

### Distributed lock giải phóng bằng Lua

```lua
if redis.call('GET', KEYS[1]) == ARGV[1] then
    return redis.call('DEL', KEYS[1])
end
```

Nếu chỉ `DEL` không kiểm tra: instance A giữ khóa, khóa hết hạn, instance B giành được,
A xong việc và `DEL` → xóa nhầm khóa của B. So khớp token giải quyết việc này.

---

## 8. SignalR + Redis backplane

```
   Client X ──WebSocket──▶ notification-service #1 ─┐
                                                     ├──▶ Redis pub/sub
   Client Y ──WebSocket──▶ notification-service #2 ─┘
```

Không có backplane: instance #1 nhận event Kafka, đẩy cho client đang nối vào #1 — client
nối vào #2 không nhận được gì. Redis backplane chuyển tiếp message giữa các instance.

### Nhóm (group)

Mỗi client khi kết nối được thêm vào:
- `user:{userId}` — thông báo cá nhân,
- `role:{role}` — cảnh báo vận hành (ví dụ hàng sắp hết chỉ gửi cho `role:Manager`).

### Token qua WebSocket

WebSocket không gửi được header `Authorization`. SignalR đặt token vào query string, phía server
`JwtBearerEvents.OnMessageReceived` đọc ra — chỉ áp dụng cho đường dẫn `/hubs`:

```csharp
var accessToken = context.Request.Query["access_token"];
if (!string.IsNullOrEmpty(accessToken) &&
    context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
    context.Token = accessToken;
```

Giới hạn theo path là quan trọng: nếu chấp nhận token qua query string cho mọi endpoint,
token sẽ lọt vào access log của proxy và trình duyệt.

---

## 9. Mô hình dữ liệu

Mỗi service một schema riêng trong Postgres:

| Service | Schema | Bảng chính |
|---|---|---|
| identity | `identity` | `users`, `refresh_tokens` |
| product | `catalog` | `products`, `categories` |
| inventory | `inventory` | `stock_items`, `reservations`, `reservation_lines`, `stock_movements` |
| order | `ordering` | `orders`, `order_lines` |
| payment | `payment` | `payments`, `refunds` |
| notification | `notification` | `notifications` |

Mọi schema đều có thêm `outbox_messages` và `inbox_messages`.

### Owned type cho Money

`Money` là value object (số tiền + đơn vị) nhưng không cần bảng riêng:

```csharp
builder.OwnsOne(x => x.Price, price =>
{
    price.Property(m => m.Amount).HasColumnName("price").HasPrecision(18, 2);
    price.Property(m => m.Currency).HasColumnName("currency").HasMaxLength(3);
});
```

Lưu phẳng thành 2 cột trong cùng bảng — vừa giữ được tính đóng gói ở tầng domain,
vừa không phải join.

### Dòng hàng chụp lại giá

`order_lines` lưu `product_name` và `unit_price` tại thời điểm đặt, **không** tham chiếu sang
catalog. Giá sản phẩm đổi sau này không được làm thay đổi đơn hàng cũ — đó là dữ liệu lịch sử.

---

## 10. Bảo mật

| Lớp | Cơ chế |
|---|---|
| Xác thực | JWT HS256, access token 60 phút |
| Phiên dài | Refresh token 7 ngày, **xoay vòng** — token cũ bị thu hồi khi cấp token mới |
| Mật khẩu | BCrypt work factor 12 |
| Phân quyền | `[Authorize(Roles = "...")]` + kiểm tra quyền sở hữu trong handler |
| Rate limit | Gateway: 300 req/phút/IP, riêng `/api/auth` là 20 req/phút |
| Chống dò email | Đăng nhập sai trả **cùng một lỗi** cho "không có user" và "sai mật khẩu" |
| Khóa tài khoản | Thu hồi toàn bộ refresh token ngay lập tức |
| Đổi mật khẩu | Thu hồi toàn bộ phiên cũ |

### Kiểm tra quyền sở hữu, không chỉ vai trò

```csharp
var isStaff = currentUser.Roles.Any(r => r is "Admin" or "Manager");
if (!isStaff && order.CustomerId != currentUser.UserId)
    return Result.Failure<OrderDto>(OrderErrors.Forbidden);
```

`[Authorize]` chỉ đảm bảo "đã đăng nhập". Không có đoạn trên thì khách A đoán được id đơn hàng
của khách B là xem được.

Với danh sách, quyền được **ép ở tầng query** chứ không lọc sau:

```csharp
var customerId = isStaff ? query.CustomerId : currentUser.UserId;
```

Nếu chỉ dựa vào tham số client gửi lên, khách chỉ cần đổi `?customerId=` là xem được đơn người khác.
