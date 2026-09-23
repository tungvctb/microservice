/** Envelope thống nhất mà mọi service .NET trả về. */
export interface ApiResponse<T> {
  success: boolean
  data?: T
  error?: ApiError
  correlationId?: string
  timestamp: string
}

export interface ApiError {
  code: string
  message: string
  details?: string[]
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
  hasNext: boolean
  hasPrevious: boolean
}

// ----------------------------------------------------------------- identity --
export interface User {
  id: string
  email: string
  fullName: string
  phone?: string | null
  roles: string[]
  isActive: boolean
  createdAtUtc: string
  lastLoginAtUtc?: string | null
}

export interface AuthResult {
  accessToken: string
  refreshToken: string
  expiresInSeconds: number
  user: User
}

// ------------------------------------------------------------------ catalog --
export interface Product {
  id: string
  sku: string
  name: string
  description?: string | null
  price: number
  currency: string
  categoryId: string
  categoryName?: string | null
  isActive: boolean
  imageUrl?: string | null
  createdAtUtc: string
  updatedAtUtc?: string | null
}

export interface ProductSummary {
  id: string
  sku: string
  name: string
  price: number
  currency: string
  categoryName?: string | null
  isActive: boolean
  imageUrl?: string | null
}

export interface Category {
  id: string
  name: string
  slug: string
  description?: string | null
  isActive: boolean
  productCount: number
  createdAtUtc: string
}

// ---------------------------------------------------------------- inventory --
export interface StockItem {
  id: string
  productId: string
  sku: string
  warehouseCode: string
  quantityOnHand: number
  quantityReserved: number
  quantityAvailable: number
  reorderLevel: number
  isLowStock: boolean
  createdAtUtc: string
  updatedAtUtc?: string | null
}

export type StockMovementType = 'Inbound' | 'Outbound' | 'Reserved' | 'Released' | 'Adjustment'

export interface StockMovement {
  id: string
  productId: string
  sku: string
  type: StockMovementType
  quantity: number
  quantityAfter: number
  reference?: string | null
  note?: string | null
  createdAtUtc: string
}

// ------------------------------------------------------------------- orders --
export type OrderStatus =
  | 'Pending' | 'StockReserved' | 'AwaitingPayment' | 'Paid'
  | 'Confirmed' | 'Shipped' | 'Completed' | 'Cancelled' | 'Failed'

export type PaymentMethodType = 'CreditCard' | 'BankTransfer' | 'Cod' | 'Wallet'

export interface ShippingAddress {
  recipientName: string
  phone: string
  street: string
  ward: string
  district: string
  city: string
  note?: string | null
}

export interface OrderLine {
  productId: string
  sku: string
  productName: string
  quantity: number
  unitPrice: number
  lineTotal: number
  currency: string
}

export interface Order {
  id: string
  orderNumber: string
  customerId: string
  customerEmail: string
  status: OrderStatus
  paymentMethod: PaymentMethodType
  totalAmount: number
  currency: string
  shippingAddress: ShippingAddress
  lines: OrderLine[]
  reservationId?: string | null
  paymentId?: string | null
  paymentRef?: string | null
  cancellationReason?: string | null
  createdAtUtc: string
  confirmedAtUtc?: string | null
  completedAtUtc?: string | null
}

export interface OrderSummary {
  id: string
  orderNumber: string
  status: OrderStatus
  totalAmount: number
  currency: string
  itemCount: number
  createdAtUtc: string
}

export interface OrderStatistics {
  totalOrders: number
  pendingOrders: number
  confirmedOrders: number
  cancelledOrders: number
  totalRevenue: number
}

// ----------------------------------------------------------------- payments --
export type PaymentStatus =
  | 'Pending' | 'Authorized' | 'Captured' | 'Failed' | 'Refunded' | 'PartiallyRefunded'

export interface Refund {
  id: string
  amount: number
  currency: string
  reason: string
  transactionRef?: string | null
  createdAtUtc: string
}

export interface Payment {
  id: string
  orderId: string
  customerId: string
  amount: number
  currency: string
  method: PaymentMethodType
  status: PaymentStatus
  transactionRef?: string | null
  failureReason?: string | null
  refundedAmount: number
  refundableAmount: number
  createdAtUtc: string
  completedAtUtc?: string | null
  refunds: Refund[]
}

// ------------------------------------------------------------ notifications --
export type NotificationSeverity = 'Info' | 'Success' | 'Warning' | 'Error'

export interface AppNotification {
  id: string
  recipientId?: string | null
  title: string
  body: string
  severity: NotificationSeverity
  channel: string
  link?: string | null
  metadata: Record<string, string>
  isRead: boolean
  readAtUtc?: string | null
  createdAtUtc: string
}
