# E-Commerce Microservices — Dự án thử nghiệm

Hệ thống thương mại điện tử dựng theo kiến trúc microservices để **học và thử nghiệm** các kỹ thuật
phân tán: Clean Architecture, gRPC, Kafka, Saga, Outbox, Redis, SignalR.

| Thành phần | Công nghệ |
|---|---|
| Backend | ASP.NET Core 8, EF Core 8, PostgreSQL 16 |
| Giao tiếp đồng bộ | **gRPC** (service ↔ service) |
| Giao tiếp bất đồng bộ | **Apache Kafka** (KRaft mode, không cần Zookeeper) |
| Cache / khóa phân tán | **Redis 7** |
| Realtime | **SignalR** + Redis backplane |
| API Gateway | YARP |
| Frontend | **React 19**, Vite 6, TypeScript, TanStack Query, Tailwind |
| Đóng gói | Docker Compose |

---

## 1. Tư vấn: vì sao lại là 6 service này?

Bạn đề xuất 4 service (Product, Order, Payment, Inventory). Tôi giữ nguyên và **tách thêm 2**:

| Service | Cổng | Vì sao tồn tại độc lập |
|---|---|---|
| **identity-service** | 5100 | Không thể gắn xác thực vào Product hay Order — mọi service đều cần nó. Tách riêng thì đổi cơ chế auth (JWT → OIDC) không đụng service nghiệp vụ. |
| **product-service** | 5101 | Catalog: **đọc nhiều, ghi ít**. Cache mạnh, scale đọc riêng. |
| **inventory-service** | 5102 | Tồn kho: **ghi nhiều, tranh chấp cao**. Nếu gộp vào Product thì mỗi lần đặt hàng lại khóa cả bảng sản phẩm. Vòng đời dữ liệu cũng khác hẳn. |
| **order-service** | 5103 | Điều phối saga. Là aggregate phức tạp nhất, cần transaction boundary riêng. |
| **payment-service** | 5104 | Dữ liệu nhạy cảm, yêu cầu tuân thủ (PCI), cần audit log chặt. Tách ra để thu hẹp phạm vi kiểm toán. |
| **notification-service** | 5105 | Giữ hàng nghìn WebSocket connection — đặc tính tài nguyên khác hẳn service REST. Scale theo số người online, không theo lượng đơn. |
| **api-gateway** | 5000 | Một entry point cho FE: JWT, CORS, rate limit, routing. FE không cần biết có bao nhiêu service. |

> **Nguyên tắc tách**: mỗi service sở hữu database riêng (database-per-service).
> Không service nào được đọc bảng của service khác — muốn dữ liệu thì gọi gRPC hoặc nghe event.

---

## 2. Cấu trúc source

```
ecommerce-microservices/
│
├── Directory.Build.props          # TargetFramework, nullable, langversion — áp cho MỌI project
├── Directory.Packages.props       # Central Package Management: khai báo version NuGet 1 chỗ duy nhất
├── docker-compose.yml             # Toàn bộ hệ thống: hạ tầng + 6 service + gateway + FE
├── .env.example                   # Biến môi trường (copy thành .env)
│
├── shared/BuildingBlocks/         # ── Thư viện dùng chung cho mọi service ──
│   ├── BuildingBlocks.Core/           # Không phụ thuộc gì (kể cả ASP.NET)
│   │   ├── Domain/                    #   Entity, AggregateRoot, ValueObject, Money, IDomainEvent
│   │   ├── Results/                   #   Result<T> + Error — thay cho ném exception cho lỗi nghiệp vụ
│   │   ├── Pagination/                #   PagedResult<T>, PageRequest
│   │   └── Abstractions/              #   IUnitOfWork, IDateTimeProvider
│   │
│   ├── BuildingBlocks.Application/    # Phụ thuộc Core + Contracts
│   │   ├── Cqrs/                      #   ICommand/IQuery/IDispatcher — CQRS tự viết, KHÔNG dùng MediatR
│   │   ├── Behaviors/                 #   Validation + Logging chạy quanh mỗi handler
│   │   ├── Caching/                   #   ICacheService, IDistributedLock (interface)
│   │   ├── Messaging/                 #   IIntegrationEventHandler, IOutboxWriter
│   │   └── Security/                  #   ICurrentUser
│   │
│   ├── BuildingBlocks.Contracts/      # HỢP ĐỒNG công khai giữa các service
│   │   ├── Protos/                    #   *.proto — gRPC (sinh cả client lẫn server stub)
│   │   └── IntegrationEvents/         #   Event Kafka + KafkaTopics
│   │
│   └── BuildingBlocks.Infrastructure/ # Hiện thực hạ tầng
│       ├── Kafka/                     #   Producer, ConsumerService, DLQ, IntegrationEventRegistry
│       ├── Outbox/                    #   Transactional Outbox + Inbox (chống xử lý trùng)
│       ├── Redis/                     #   RedisCacheService, RedisDistributedLock
│       ├── Persistence/               #   Interceptor: audit, domain event, outbox
│       ├── Grpc/                      #   Interceptor propagate correlation-id + JWT
│       ├── Web/                       #   ApiResponse, ApiControllerBase, middleware
│       └── ServiceDefaults.cs         #   ⭐ Một lệnh gọi cấu hình xong toàn bộ nền của service
│
├── services/                      # ── 6 service, mỗi cái 1 SOLUTION riêng ──
│   ├── identity/      Identity.sln
│   ├── product/       Product.sln
│   ├── inventory/     Inventory.sln
│   ├── order/         Order.sln
│   ├── payment/       Payment.sln
│   └── notification/  Notification.sln
│
├── gateway/           Gateway.sln    # YARP reverse proxy
│
├── frontend/                      # React 19 + Vite
│   └── src/
│       ├── lib/                       # api-client (axios + auto refresh token), types, format
│       ├── components/ui/             # Button, Table, Modal, Toast, Pagination...
│       ├── features/                  # Chia theo NGHIỆP VỤ, không theo loại file
│       │   ├── auth/  products/  categories/  inventory/
│       │   ├── orders/  payments/  notifications/  users/  dashboard/
│       └── layouts/                   # AppLayout (sidebar + header + chuông thông báo)
│
├── deploy/postgres/               # Script tạo 6 database khi container Postgres khởi động lần đầu
└── docs/                          # Tài liệu chi tiết
```

### Bên trong mỗi service (Clean Architecture 4 tầng)

Lấy `services/product/` làm ví dụ — **5 service còn lại có cấu trúc y hệt**:

```
services/product/
├── Product.sln                    # Solution gồm 4 project của service + 4 building block
└── src/
    ├── Product.Domain/            # ⬅ TẦNG TRONG CÙNG — không phụ thuộc gì ngoài BuildingBlocks.Core
    │   ├── Entities/              #   ProductItem, Category (aggregate root, có hành vi)
    │   ├── Events/                #   Domain event nội bộ
    │   ├── Repositories/          #   INTERFACE repository (hiện thực nằm ở Infrastructure)
    │   └── Errors/                #   Định nghĩa lỗi nghiệp vụ
    │
    ├── Product.Application/       # ⬅ Use case. Phụ thuộc Domain. KHÔNG biết EF Core hay Kafka
    │   ├── Products/
    │   │   ├── Commands/          #   CreateProduct.cs = Command + Validator + Handler trong 1 file
    │   │   └── Queries/           #   GetProductById, SearchProducts...
    │   ├── Categories/
    │   ├── EventHandlers/         #   Xử lý integration event nhận từ Kafka
    │   └── DependencyInjection.cs
    │
    ├── Product.Infrastructure/    # ⬅ Chi tiết kỹ thuật. Phụ thuộc Application
    │   ├── Persistence/
    │   │   ├── ProductDbContext.cs
    │   │   ├── Configurations/    #   Mapping EF (snake_case, index, owned type)
    │   │   ├── Repositories/      #   Hiện thực interface của Domain
    │   │   └── Migrations/        #   EF migration đã sinh sẵn
    │   ├── Messaging/             #   Map domain event → integration event
    │   └── DependencyInjection.cs
    │
    └── Product.Api/               # ⬅ TẦNG NGOÀI CÙNG
        ├── Controllers/           #   REST cho frontend
        ├── Grpc/                  #   gRPC endpoint cho service khác gọi
        ├── Program.cs             #   Lắp ráp mọi thứ
        └── appsettings.json
```

**Chiều phụ thuộc luôn hướng vào trong**: `Api → Infrastructure → Application → Domain`.
Domain không biết gì về database, HTTP hay message broker.

---

## 3. Chạy dự án

### Cách 1 — Docker Compose (khuyến nghị)

> **Trước tiên**: user của bạn phải có quyền dùng Docker daemon. Kiểm tra bằng `docker ps`.
> Nếu báo `permission denied ... /var/run/docker.sock` thì chạy:
> ```bash
> sudo usermod -aG docker $USER && newgrp docker
> ```
> (hoặc thêm `sudo` vào trước mọi lệnh `docker compose` bên dưới).

```bash
cp .env.example .env
docker compose up -d --build

# Theo dõi log
docker compose logs -f order-service payment-service
```

Lần đầu mất khoảng 5–8 phút để build image. Sau khi xong:

| Địa chỉ | Dùng để |
|---|---|
| http://localhost:5173 | **Giao diện web** |
| http://localhost:5000 | API Gateway |
| http://localhost:5000/health/services | Tình trạng toàn bộ service |
| http://localhost:8090 | Kafka UI — xem message chạy qua các topic |
| http://localhost:5101/swagger | Swagger của product-service (đổi cổng cho service khác) |

**Tài khoản mẫu** (đã seed sẵn):

| Email | Mật khẩu | Vai trò |
|---|---|---|
| admin@shop.local | `Admin@123` | Admin + Manager |
| manager@shop.local | `Manager@123` | Manager |
| customer@shop.local | `Customer@123` | Customer |

### Cách 2 — Chạy từng service bằng `dotnet run`

Chỉ bật hạ tầng bằng Docker, còn service chạy trên máy để debug:

```bash
docker compose up -d postgres redis kafka kafka-ui

# Mỗi lệnh 1 terminal
dotnet run --project services/identity/src/Identity.Api
dotnet run --project services/product/src/Product.Api
dotnet run --project services/inventory/src/Inventory.Api
dotnet run --project services/order/src/Order.Api
dotnet run --project services/payment/src/Payment.Api
dotnet run --project services/notification/src/Notification.Api
dotnet run --project gateway/src/Gateway.Api

# Frontend
cd frontend && npm install && npm run dev
```

Database và topic Kafka được tạo tự động khi service khởi động.

### Hành vi khi hạ tầng chưa sẵn sàng

Service **không chết** khi Postgres/Kafka/Redis chưa lên:

- HTTP server khởi động ngay, `/health/live` trả `200`.
- `/health/ready` trả `503` **trong vòng 3 giây**, kèm chi tiết thành phần nào hỏng:
  ```json
  { "status": "Unhealthy", "entries": {
      "postgres": { "status": "Unhealthy", "description": "Failed to connect to 127.0.0.1:5432" },
      "kafka":    { "status": "Unhealthy" },
      "redis":    { "status": "Unhealthy" } } }
  ```
- Migration retry 10 lần (3 giây/lần); hết lượt vẫn hỏng thì log `ERROR` rồi chạy tiếp ở trạng thái
  NOT READY, thay vì crash khiến orchestrator restart vòng lặp mà không rõ lý do.

---

## 4. Luồng chạy chính — Saga đặt hàng

Đây là phần đáng xem nhất của dự án: một giao dịch trải qua **4 service**, không có distributed transaction.

```
                                   ┌──────────────────┐
   POST /api/orders ──────────────▶│  order-service   │
                                   └────────┬─────────┘
                                            │
       ① gRPC: ValidateProducts             │  Chốt giá TẠI SERVER
          ◀───────────────────────  product-service     (không tin giá client gửi)
                                            │
       ② gRPC: ReserveStock                 │  Giữ chỗ all-or-nothing
          ◀───────────────────────  inventory-service   + Redis distributed lock
                                            │
       ③ Ghi đơn + Outbox (CÙNG 1 TRANSACTION)
                                            │
                                            ▼
                              Kafka: ecommerce.order.events
                                     "order.placed"
                                            │
                                            ▼
                                   ┌──────────────────┐
                                   │ payment-service  │  ④ Tự động thu tiền
                                   └────────┬─────────┘     (idempotent theo orderId)
                                            │
                       ┌────────────────────┴────────────────────┐
                       ▼                                         ▼
          Kafka "payment.succeeded"                  Kafka "payment.failed"
                       │                                         │
        ┌──────────────┼──────────────┐                          ▼
        ▼              ▼              ▼                  order-service hủy đơn
   order-service  inventory-    notification-                     │
   → Confirmed    service       service                           ▼
                  → Trừ kho     → SignalR         Kafka "order.cancelled"
                    thật          đẩy realtime               │
                                                             ▼
                                                    inventory-service
                                                    → NHẢ hàng giữ chỗ (bồi hoàn)
```

### Muốn xem nhánh thất bại?

Cổng thanh toán giả lập được cấu hình để **từ chối mọi đơn có tổng tiền chia hết cho 13**.
Đặt một đơn với tổng tiền như vậy, bạn sẽ thấy:

1. Đơn chuyển sang `Cancelled` sau vài giây,
2. Số lượng "đang giữ chỗ" ở màn Tồn kho tự trở về 0,
3. Thông báo lỗi hiện lên **realtime** ở góc màn hình (không cần F5).

Ngoài ra `PaymentGateway__FailureRate` (mặc định `0.1`) tạo lỗi ngẫu nhiên 10%.

---

## 5. Các kỹ thuật phân tán trong dự án

### Transactional Outbox — không bao giờ mất event

Vấn đề kinh điển: ghi DB xong thì service chết trước khi kịp publish Kafka → event bốc hơi.

Cách giải ở đây: `OutboxDomainEventInterceptor` chạy **trước** `SaveChanges`, chuyển domain event
thành dòng trong bảng `outbox_messages` — **cùng transaction** với dữ liệu nghiệp vụ.
Một background job (`OutboxProcessor`) quét bảng này và đẩy lên Kafka sau.

> `shared/BuildingBlocks/BuildingBlocks.Infrastructure/Outbox/`

### Inbox — chống xử lý trùng

Kafka đảm bảo *at-least-once*: message có thể được giao lại. Consumer ghi `EventId` đã xử lý
vào bảng `inbox_messages` (unique index), gặp lại thì bỏ qua.

### Idempotency ở Payment

Ngoài Inbox, `payments.idempotency_key` và `payments.order_id` đều có **unique index** — tuyến
phòng thủ cuối cùng chống trừ tiền 2 lần, kể cả khi mọi lớp trên đều hỏng.

### Redis distributed lock

Nhiều đơn hàng cùng tranh 1 sản phẩm → khóa theo `productId`, **sắp xếp productId trước khi khóa**
để tránh deadlock. Giải phóng bằng Lua script so khớp token (không xóa nhầm khóa của instance khác).

> `shared/BuildingBlocks/BuildingBlocks.Infrastructure/Redis/RedisDistributedLock.cs`

### Optimistic concurrency

Mọi aggregate dùng cột `xmin` của PostgreSQL làm concurrency token. Hai request cùng sửa 1 bản ghi
thì request chậm hơn sẽ fail và phải đọc lại — không có lost update.

### Reservation TTL

Giữ chỗ có hạn 15 phút. `ReservationExpiryJob` quét và nhả hàng quá hạn — nếu không, một đơn bị bỏ
dở sẽ khóa hàng vĩnh viễn.

### Dead Letter Queue

Message xử lý lỗi quá 3 lần được đẩy sang topic `*.dlq` kèm header lý do, thay vì chặn cả partition.

### Correlation ID

Gateway sinh `X-Correlation-Id` cho mỗi request, propagate qua HTTP header → gRPC metadata →
Kafka header. Grep một correlation id trong log là thấy toàn bộ hành trình qua 4 service.

---

## 6. gRPC — ai gọi ai

Proto đặt tập trung ở `shared/BuildingBlocks/BuildingBlocks.Contracts/Protos/`, sinh **cả client lẫn
server stub** nên mọi service dùng chung đúng một định nghĩa.

| Từ | Tới | Method | Vì sao dùng gRPC thay vì Kafka |
|---|---|---|---|
| order | product | `ValidateProducts` | Cần **kết quả ngay** để trả lời khách |
| order | inventory | `ReserveStock` | Phải biết còn hàng hay không trước khi tạo đơn |
| order | inventory | `CommitReservation` / `ReleaseReservation` | Thao tác điểm, cần phản hồi |
| bất kỳ | notification | `PushToUser` | Khi độ trễ quan trọng hơn khả năng chịu lỗi |

**Quy tắc**: cần câu trả lời ngay → gRPC. Thông báo "việc này đã xảy ra" → Kafka.

---

## 7. Topic Kafka

| Topic | Producer | Consumer |
|---|---|---|
| `ecommerce.product.events` | product | inventory (tạo tồn kho cho sản phẩm mới) |
| `ecommerce.inventory.events` | inventory | notification (cảnh báo sắp hết hàng) |
| `ecommerce.order.events` | order | payment, inventory, notification |
| `ecommerce.payment.events` | payment | order, inventory, notification |
| `ecommerce.notification.commands` | bất kỳ | notification |
| `*.dlq` | hệ thống | con người 🙂 |

Message key = `AggregateId` → mọi event của cùng một đơn hàng vào cùng partition, **giữ đúng thứ tự**.

Mở http://localhost:8090 để xem message chạy qua theo thời gian thực.

---

## 8. Frontend

```
npm run dev      # chạy dev server, proxy /api và /hubs sang gateway
npm run build    # type-check (tsc -b) rồi build production
```

Điểm đáng chú ý:

- **Auto refresh token**: nhiều request cùng dính 401 chỉ kích hoạt **một** lần gọi `/auth/refresh`,
  các request còn lại xếp hàng chờ rồi phát lại (`src/lib/api-client.ts`).
- **SignalR**: `useNotificationHub` tự kết nối lại theo backoff, token đi qua `accessTokenFactory`.
  Khi nhận event, nó invalidate luôn cache TanStack Query của orders/stocks để UI tự đồng bộ.
- **Phân quyền ở UI**: menu và nút bấm ẩn theo vai trò, nhưng **server vẫn kiểm tra lại** —
  ẩn ở UI chỉ là trải nghiệm, không phải bảo mật.
- Mọi màn hình đều có **CRUD đầy đủ**: tạo, sửa, xóa, tìm kiếm, lọc, phân trang.

---

## 9. Lệnh hữu ích

```bash
# Build toàn bộ backend
for s in services/*/*.sln gateway/*.sln; do dotnet build "$s"; done

# Sinh migration mới cho 1 service
dotnet ef migrations add TenMigration \
  --project services/product/src/Product.Infrastructure \
  --startup-project services/product/src/Product.Api \
  --output-dir Persistence/Migrations

# Xem log 1 service
docker compose logs -f order-service

# Xóa sạch dữ liệu, làm lại từ đầu
docker compose down -v && docker compose up -d --build

# Kiểm tra sức khỏe toàn hệ thống
curl -s http://localhost:5000/health/services | jq
```

---

## 10. Đọc tiếp

| Tài liệu | Nội dung |
|---|---|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Giải thích sâu từng quyết định thiết kế |
| [docs/API.md](docs/API.md) | Danh sách endpoint REST đầy đủ |
| [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) | Thêm service mới, quy ước code, xử lý sự cố |

---

## ⚠️ Lưu ý: đây là dự án thử nghiệm

Những thứ **chưa** phù hợp cho production:

- `Jwt:SecretKey` nằm trong `appsettings.json` → phải chuyển sang secret store.
- Một Postgres cho cả 6 database → production nên tách instance.
- Kafka 1 broker, replication factor = 1 → không chịu được mất node.
- `AutoCreateTopics` bật → production nên quản lý topic bằng IaC.
- Cổng thanh toán là **giả lập** — thay `IPaymentGateway` bằng VNPay/Stripe khi dùng thật.
- Chưa có distributed tracing (OpenTelemetry đã khai báo package, chưa nối vào collector).
