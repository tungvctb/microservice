# Hướng dẫn phát triển

## Yêu cầu môi trường

| Công cụ | Phiên bản | Bắt buộc? |
|---|---|---|
| .NET SDK | 8.0+ | Có |
| Node.js | 20+ (dự án dùng 22) | Có |
| Docker + Compose | v2+ | Có |
| dotnet-ef | 8.0.11 | Đã cài sẵn dạng local tool |

```bash
dotnet tool restore      # khôi phục dotnet-ef từ .config/dotnet-tools.json
```

---

## Quy ước code

### Backend

| Chủ đề | Quy ước |
|---|---|
| Một use case | 1 file chứa cả Command + Validator + Handler (`CreateProduct.cs`) |
| Handler | `internal sealed class` — không ai ngoài assembly cần gọi trực tiếp |
| Command/Query | `public sealed record` |
| Trả về | `Result<T>` cho lỗi nghiệp vụ; exception chỉ cho sự cố hệ thống |
| Tên bảng/cột | `snake_case`, khai báo tường minh trong `IEntityTypeConfiguration` |
| Entity | Constructor `private`, tạo qua factory method tĩnh |
| Setter | `private set` — thay đổi trạng thái phải đi qua method có tên nghiệp vụ |
| Comment | Chỉ giải thích **vì sao**, không mô tả lại code |

### Frontend

| Chủ đề | Quy ước |
|---|---|
| Tổ chức | Chia theo **feature**, không theo loại file |
| Gọi API | Gom vào `*-api.ts` mỗi feature, không gọi axios rải rác trong component |
| Server state | TanStack Query (`useQuery`/`useMutation`) |
| Local state | `useState` — không dùng thư viện state management |
| Form | react-hook-form + zod, schema **khớp với validator phía server** |
| Import | Dùng alias `@/` |

---

## Thêm một use case mới

Ví dụ: thêm chức năng "nhân bản sản phẩm".

**1. Domain** — nếu cần hành vi mới:

```csharp
// services/product/src/Product.Domain/Entities/ProductItem.cs
public ProductItem Duplicate(string newSku)
{
    var copy = Create(newSku, $"{Name} (bản sao)", Description,
        Price.Amount, Price.Currency, CategoryId, ImageUrl);
    return copy;
}
```

**2. Application** — tạo file use case:

```csharp
// services/product/src/Product.Application/Products/Commands/DuplicateProduct.cs
public sealed record DuplicateProductCommand(Guid SourceId, string NewSku) : ICommand<ProductDto>;

public sealed class DuplicateProductValidator : AbstractValidator<DuplicateProductCommand>
{
    public DuplicateProductValidator()
    {
        RuleFor(x => x.NewSku).NotEmpty().MaximumLength(50);
    }
}

internal sealed class DuplicateProductHandler(
    IProductRepository products, IUnitOfWork unitOfWork)
    : ICommandHandler<DuplicateProductCommand, ProductDto>
{
    public async Task<Result<ProductDto>> Handle(DuplicateProductCommand command, CancellationToken ct)
    {
        // ...
    }
}
```

Không cần đăng ký DI thủ công — `AddCqrs()` quét assembly và tự đăng ký handler lẫn validator.

**3. Api** — thêm endpoint:

```csharp
[HttpPost("{id:guid}/duplicate")]
[Authorize(Roles = "Admin,Manager")]
public async Task<IActionResult> Duplicate(Guid id, [FromBody] DuplicateRequest request, CancellationToken ct)
    => ToResponse(await Dispatcher.Send(new DuplicateProductCommand(id, request.NewSku), ct));
```

**4. Frontend** — thêm vào `product-api.ts` rồi dùng `useMutation`.

---

## Thêm một integration event mới

**1.** Khai báo hợp đồng ở `shared/BuildingBlocks/BuildingBlocks.Contracts/IntegrationEvents/`:

```csharp
public sealed record ProductArchivedIntegrationEvent : IntegrationEvent
{
    public override string EventType => "product.archived";   // tiền tố quyết định topic
    public override string AggregateId => ProductId.ToString();
    public Guid ProductId { get; init; }
}
```

> `TopicResolver` lấy phần trước dấu `.` của `EventType` để chọn topic. Tiền tố mới phải
> được thêm vào `TopicResolver.For()`.

**2.** Map từ domain event trong `*IntegrationEventMapper.cs` của service phát.

**3.** Ở service nhận, viết handler:

```csharp
public sealed class ProductArchivedHandler(IDispatcher dispatcher)
    : IIntegrationEventHandler<ProductArchivedIntegrationEvent>
{
    public Task HandleAsync(ProductArchivedIntegrationEvent @event, CancellationToken ct) => /* ... */;
}
```

**4.** Đảm bảo service nhận có subscribe topic tương ứng trong `Program.cs`:

```csharp
.AddKafkaMessaging(
    consumerGroupId: "inventory-service",
    topics: new[] { KafkaTopics.ProductEvents, ... },
    typeof(DependencyInjection).Assembly)
```

Handler được quét và đăng ký tự động.

---

## Thêm một service hoàn toàn mới

```bash
NAME=Shipping
DIR=services/shipping
mkdir -p $DIR/src/$NAME.{Domain,Application,Infrastructure,Api}
```

1. Sao chép 4 file `.csproj` từ một service có sẵn, đổi tên.
2. Tạo solution:
   ```bash
   cd $DIR && dotnet new sln -n $NAME
   dotnet sln add src/*/*.csproj ../../shared/BuildingBlocks/*/*.csproj
   ```
3. `Program.cs` — dùng lại `ServiceDefaults`:
   ```csharp
   builder
       .AddServiceDefaults("shipping-service")
       .AddJwtAuthentication()
       .AddRedisInfrastructure("shipping")
       .AddKafkaMessaging("shipping-service", new[] { KafkaTopics.OrderEvents }, typeof(Program).Assembly)
       .AddShippingInfrastructure()
       .AddOutboxProcessing<ShippingDbContext>();
   ```
4. `DbContext` phải implement `IIntegrationDbContext` và gọi `modelBuilder.ApplyIntegrationTables()`.
5. Thêm database vào `deploy/postgres/init-databases.sh`.
6. Thêm service + route vào `docker-compose.yml` và `gateway/src/Gateway.Api/appsettings.json`.
7. Sinh migration:
   ```bash
   dotnet ef migrations add InitialShipping \
     --project $DIR/src/$NAME.Infrastructure \
     --startup-project $DIR/src/$NAME.Api \
     --output-dir Persistence/Migrations
   ```

---

## EF Core migration

```bash
# Tạo mới
dotnet ef migrations add TenMigration \
  --project services/product/src/Product.Infrastructure \
  --startup-project services/product/src/Product.Api \
  --output-dir Persistence/Migrations

# Gỡ migration cuối (chưa apply)
dotnet ef migrations remove \
  --project services/product/src/Product.Infrastructure \
  --startup-project services/product/src/Product.Api

# Xem SQL sẽ chạy
dotnet ef migrations script \
  --project services/product/src/Product.Infrastructure \
  --startup-project services/product/src/Product.Api
```

Migration được **apply tự động khi service khởi động** (`*DbInitializer.InitializeAsync`),
có retry 10 lần vì Postgres có thể chưa sẵn sàng.

---

## Xử lý sự cố

### Service báo "Chưa kết nối được Postgres"

Bình thường trong 10–30 giây đầu — service retry 10 lần, mỗi lần cách 3 giây.
Nếu quá lâu:

```bash
docker compose ps postgres
docker compose logs postgres | tail -30
```

### Kafka: "Không kết nối được Kafka"

Kafka mất khoảng 30 giây để sẵn sàng (`start_period: 30s` trong healthcheck).

```bash
docker compose exec kafka kafka-broker-api-versions --bootstrap-server localhost:29092
```

### Event không tới service nhận

Kiểm tra theo thứ tự:

1. **Outbox có ghi không?**
   ```sql
   SELECT event_type, processed_on_utc, retry_count, error
   FROM ordering.outbox_messages ORDER BY occurred_on_utc DESC LIMIT 10;
   ```
   `processed_on_utc IS NULL` + `retry_count > 0` → lỗi publish, xem cột `error`.

2. **Message có lên Kafka không?** Mở http://localhost:8090, xem topic tương ứng.

3. **Service nhận có subscribe topic đó không?** Kiểm tra `topics:` trong `Program.cs`.

4. **Có handler cho event đó không?** Class phải implement
   `IIntegrationEventHandler<TEvent>` và nằm trong assembly đã truyền vào `AddKafkaMessaging`.

5. **Bị coi là trùng?**
   ```sql
   SELECT * FROM inventory.inbox_messages WHERE event_id = '<guid>';
   ```

6. **Vào DLQ rồi?** Xem topic `*.dlq` trên Kafka UI, header `dlq-reason` cho biết lý do.

### Đơn hàng kẹt ở `AwaitingPayment`

Payment service chưa consume được `order.placed`:

```bash
docker compose logs payment-service | tail -50
```

Sau 15 phút, `ReservationExpiryJob` sẽ tự nhả kho.

### SignalR không kết nối

- Console trình duyệt báo 401 → token hết hạn, đăng nhập lại.
- Báo CORS → kiểm tra `Cors:AllowedOrigins` có chứa origin của FE và `AllowCredentials()` đang bật.
- Qua nginx (chế độ Docker) → đảm bảo `location /hubs/` có `proxy_set_header Upgrade`.

### Xóa sạch làm lại

```bash
docker compose down -v      # -v xóa luôn volume (mất toàn bộ dữ liệu)
docker compose up -d --build
```

---

## Kiểm thử thủ công luồng saga

```bash
# 1. Lấy token
TOKEN=$(curl -s -X POST http://localhost:5000/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"customer@shop.local","password":"Customer@123"}' \
  | jq -r '.data.accessToken')

# 2. Lấy 1 productId
PID=$(curl -s "http://localhost:5000/api/products?pageSize=1" | jq -r '.data.items[0].id')

# 3. Xem tồn kho TRƯỚC khi đặt
curl -s "http://localhost:5000/api/stocks/product/$PID" | jq '.data | {onHand:.quantityOnHand, reserved:.quantityReserved}'

# 4. Đặt hàng
curl -s -X POST http://localhost:5000/api/orders \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d "{\"lines\":[{\"productId\":\"$PID\",\"quantity\":1}],
       \"shippingAddress\":{\"recipientName\":\"Test\",\"phone\":\"0901234567\",
       \"street\":\"1 Test\",\"ward\":\"P1\",\"district\":\"Q1\",\"city\":\"HCM\"},
       \"paymentMethod\":\"CreditCard\"}" | jq '.data | {orderNumber, status, totalAmount}'

# 5. Xem tồn kho NGAY SAU khi đặt → quantityReserved tăng lên
curl -s "http://localhost:5000/api/stocks/product/$PID" | jq '.data | {onHand:.quantityOnHand, reserved:.quantityReserved}'

# 6. Đợi vài giây cho saga chạy xong, xem lại
sleep 8
curl -s "http://localhost:5000/api/orders?pageSize=1" \
  -H "Authorization: Bearer $TOKEN" | jq '.data.items[0] | {orderNumber, status}'

# Thành công → status "Confirmed", reserved về 0, onHand giảm
# Thất bại   → status "Cancelled", reserved về 0, onHand KHÔNG đổi
```

Mở giao diện web song song để thấy thông báo realtime nhảy lên đúng lúc bước 6 hoàn tất.
