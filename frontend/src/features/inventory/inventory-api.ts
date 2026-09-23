import { api } from '@/lib/api-client'
import type { PagedResult, StockItem, StockMovement } from '@/lib/types'

export interface StockQuery {
  search?: string
  lowStockOnly?: boolean
  warehouseCode?: string
  page?: number
  pageSize?: number
}

export const inventoryApi = {
  search: (query: StockQuery = {}) => api.get<PagedResult<StockItem>>('/stocks', { params: query }),

  lowStock: () => api.get<StockItem[]>('/stocks/low-stock'),

  getByProduct: (productId: string) => api.get<StockItem>(`/stocks/product/${productId}`),

  movements: (productId: string, page = 1, pageSize = 20) =>
    api.get<PagedResult<StockMovement>>(`/stocks/product/${productId}/movements`, {
      params: { page, pageSize },
    }),

  receive: (productId: string, input: { quantity: number; reference?: string; note?: string }) =>
    api.post<StockItem>(`/stocks/product/${productId}/receive`, input),

  adjust: (productId: string, input: { newQuantityOnHand: number; reason: string }) =>
    api.post<StockItem>(`/stocks/product/${productId}/adjust`, input),

  setReorderLevel: (productId: string, reorderLevel: number) =>
    api.patch<StockItem>(`/stocks/product/${productId}/reorder-level`, { reorderLevel }),
}
