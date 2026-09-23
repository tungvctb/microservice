import { api } from '@/lib/api-client'
import type { Category, PagedResult, Product, ProductSummary } from '@/lib/types'

export interface ProductQuery {
  search?: string
  categoryId?: string
  isActive?: boolean
  minPrice?: number
  maxPrice?: number
  sortBy?: 'createdAt' | 'name' | 'price' | 'sku'
  descending?: boolean
  page?: number
  pageSize?: number
}

export interface CreateProductInput {
  sku: string
  name: string
  description?: string
  price: number
  currency: string
  categoryId: string
  imageUrl?: string
  initialStock: number
}

export interface UpdateProductInput {
  name: string
  description?: string
  categoryId: string
  imageUrl?: string
}

export const productApi = {
  search: (query: ProductQuery = {}) =>
    api.get<PagedResult<ProductSummary>>('/products', { params: query }),

  getById: (id: string) => api.get<Product>(`/products/${id}`),

  create: (input: CreateProductInput) => api.post<Product>('/products', input),

  update: (id: string, input: UpdateProductInput) => api.put<Product>(`/products/${id}`, input),

  changePrice: (id: string, newPrice: number, currency = 'VND') =>
    api.patch<Product>(`/products/${id}/price`, { newPrice, currency }),

  setStatus: (id: string, isActive: boolean) =>
    api.patch<Product>(`/products/${id}/status`, { isActive }),

  remove: (id: string) => api.delete(`/products/${id}`),
}

export const categoryApi = {
  list: (onlyActive = false) => api.get<Category[]>('/categories', { params: { onlyActive } }),
  getById: (id: string) => api.get<Category>(`/categories/${id}`),
  create: (input: { name: string; description?: string }) => api.post<Category>('/categories', input),
  update: (id: string, input: { name: string; description?: string; isActive: boolean }) =>
    api.put<Category>(`/categories/${id}`, input),
  remove: (id: string) => api.delete(`/categories/${id}`),
}
