import { api } from '@/lib/api-client'
import type { PagedResult, Payment } from '@/lib/types'

export interface PaymentQuery {
  orderId?: string
  customerId?: string
  status?: string
  method?: string
  page?: number
  pageSize?: number
}

export const paymentApi = {
  search: (query: PaymentQuery = {}) => api.get<PagedResult<Payment>>('/payments', { params: query }),
  getById: (id: string) => api.get<Payment>(`/payments/${id}`),
  getByOrder: (orderId: string) => api.get<Payment>(`/payments/order/${orderId}`),
  capture: (id: string) => api.post<Payment>(`/payments/${id}/capture`),
  refund: (id: string, amount: number, reason: string) =>
    api.post<Payment>(`/payments/${id}/refund`, { amount, reason }),
}
