import { api } from '@/lib/api-client'
import type { AppNotification, PagedResult } from '@/lib/types'

export interface NotificationQuery {
  isRead?: boolean
  severity?: string
  page?: number
  pageSize?: number
}

export const notificationApi = {
  list: (query: NotificationQuery = {}) =>
    api.get<PagedResult<AppNotification>>('/notifications', { params: query }),

  unreadCount: () => api.get<number>('/notifications/unread-count'),

  markRead: (id: string) => api.post<AppNotification>(`/notifications/${id}/read`),

  markAllRead: () => api.post<number>('/notifications/read-all'),

  remove: (id: string) => api.delete(`/notifications/${id}`),

  create: (input: {
    recipientId?: string | null
    title: string
    body: string
    severity?: string
    link?: string
    targetRole?: string
  }) => api.post<AppNotification>('/notifications', input),
}
