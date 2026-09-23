import { api } from '@/lib/api-client'
import type {
  Order, OrderStatistics, OrderSummary, PagedResult, PaymentMethodType, ShippingAddress,
} from '@/lib/types'

export interface OrderQuery {
  customerId?: string
  status?: string
  orderNumber?: string
  fromUtc?: string
  toUtc?: string
  page?: number
  pageSize?: number
}

export interface PlaceOrderInput {
  lines: { productId: string; quantity: number }[]
  shippingAddress: ShippingAddress
  paymentMethod: PaymentMethodType
  customerEmail?: string
}

export const orderApi = {
  search: (query: OrderQuery = {}) => api.get<PagedResult<OrderSummary>>('/orders', { params: query }),
  getById: (id: string) => api.get<Order>(`/orders/${id}`),
  statistics: () => api.get<OrderStatistics>('/orders/statistics'),
  place: (input: PlaceOrderInput) => api.post<Order>('/orders', input),
  cancel: (id: string, reason: string) => api.post<Order>(`/orders/${id}/cancel`, { reason }),
  ship: (id: string) => api.post<Order>(`/orders/${id}/ship`),
  complete: (id: string) => api.post<Order>(`/orders/${id}/complete`),
}
