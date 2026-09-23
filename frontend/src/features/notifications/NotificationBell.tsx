import { Bell, CheckCheck, Circle, Trash2, Wifi, WifiOff } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import clsx from 'clsx'
import { notificationApi } from './notification-api'
import { useNotificationHub } from './use-notification-hub'
import { useToast } from '@/components/ui/Toast'
import { formatRelative } from '@/lib/format'
import type { AppNotification, NotificationSeverity } from '@/lib/types'

const severityDot: Record<NotificationSeverity, string> = {
  Info: 'text-blue-500',
  Success: 'text-emerald-500',
  Warning: 'text-amber-500',
  Error: 'text-rose-500',
}

export function NotificationBell() {
  const [open, setOpen] = useState(false)
  const containerRef = useRef<HTMLDivElement>(null)
  const queryClient = useQueryClient()
  const toast = useToast()

  // Thông báo realtime nổi lên dạng toast ngay cả khi panel đang đóng.
  const { connected, unreadCount: liveUnread } = useNotificationHub({
    enabled: true,
    onNotification: (notification: AppNotification) => {
      const tone = notification.severity === 'Error' ? 'error'
        : notification.severity === 'Warning' ? 'warning'
        : notification.severity === 'Success' ? 'success' : 'info'
      toast[tone](notification.title, notification.body)
    },
  })

  const unreadQuery = useQuery({
    queryKey: ['notifications', 'unread-count'],
    queryFn: notificationApi.unreadCount,
    refetchInterval: 60_000,   // lưới an toàn nếu WebSocket rớt
  })

  const listQuery = useQuery({
    queryKey: ['notifications', { page: 1, pageSize: 15 }],
    queryFn: () => notificationApi.list({ page: 1, pageSize: 15 }),
    enabled: open,
  })

  const markRead = useMutation({
    mutationFn: notificationApi.markRead,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['notifications'] }),
  })

  const markAllRead = useMutation({
    mutationFn: notificationApi.markAllRead,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['notifications'] })
      toast.success('Đã đánh dấu tất cả là đã đọc')
    },
  })

  const remove = useMutation({
    mutationFn: notificationApi.remove,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['notifications'] }),
  })

  // Số liệu realtime được ưu tiên hơn số liệu polling.
  const unread = liveUnread ?? unreadQuery.data ?? 0

  useEffect(() => {
    if (!open) return
    const onClickOutside = (e: MouseEvent) => {
      if (!containerRef.current?.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', onClickOutside)
    return () => document.removeEventListener('mousedown', onClickOutside)
  }, [open])

  return (
    <div className="relative" ref={containerRef}>
      <button
        onClick={() => setOpen((v) => !v)}
        className="relative rounded-lg p-2 text-slate-500 transition-colors hover:bg-slate-100 hover:text-slate-700"
        aria-label={`Thông báo${unread > 0 ? ` (${unread} chưa đọc)` : ''}`}
      >
        <Bell className="h-5 w-5" />
        {unread > 0 && (
          <span className="absolute -right-0.5 -top-0.5 flex h-4.5 min-w-[18px] items-center justify-center rounded-full bg-rose-500 px-1 text-[10px] font-semibold text-white">
            {unread > 99 ? '99+' : unread}
          </span>
        )}
      </button>

      {open && (
        <div className="absolute right-0 z-50 mt-2 w-[22rem] overflow-hidden rounded-xl border border-slate-200 bg-white shadow-xl">
          <header className="flex items-center justify-between border-b border-slate-100 px-4 py-3">
            <div className="flex items-center gap-2">
              <h3 className="text-sm font-semibold text-slate-900">Thông báo</h3>
              <span
                title={connected ? 'Đang kết nối realtime' : 'Mất kết nối realtime'}
                className={clsx('flex items-center', connected ? 'text-emerald-500' : 'text-slate-300')}
              >
                {connected ? <Wifi className="h-3.5 w-3.5" /> : <WifiOff className="h-3.5 w-3.5" />}
              </span>
            </div>

            {unread > 0 && (
              <button
                onClick={() => markAllRead.mutate()}
                className="flex items-center gap-1 text-xs font-medium text-brand-600 hover:text-brand-700"
              >
                <CheckCheck className="h-3.5 w-3.5" />
                Đọc tất cả
              </button>
            )}
          </header>

          <div className="max-h-[26rem] overflow-y-auto">
            {listQuery.isLoading && (
              <p className="px-4 py-10 text-center text-sm text-slate-400">Đang tải...</p>
            )}

            {listQuery.data?.items.length === 0 && (
              <p className="px-4 py-10 text-center text-sm text-slate-400">Chưa có thông báo nào.</p>
            )}

            {listQuery.data?.items.map((item) => (
              <div
                key={item.id}
                className={clsx(
                  'group flex gap-2.5 border-b border-slate-50 px-4 py-3 last:border-0',
                  !item.isRead && 'bg-brand-50/40',
                )}
              >
                <Circle
                  className={clsx('mt-1.5 h-2 w-2 shrink-0 fill-current', severityDot[item.severity])}
                />

                <div className="min-w-0 flex-1">
                  {item.link ? (
                    <Link
                      to={item.link}
                      onClick={() => {
                        if (!item.isRead) markRead.mutate(item.id)
                        setOpen(false)
                      }}
                      className="text-sm font-medium text-slate-900 hover:text-brand-600"
                    >
                      {item.title}
                    </Link>
                  ) : (
                    <p className="text-sm font-medium text-slate-900">{item.title}</p>
                  )}

                  <p className="mt-0.5 text-xs leading-relaxed text-slate-500">{item.body}</p>
                  <p className="mt-1 text-[11px] text-slate-400">{formatRelative(item.createdAtUtc)}</p>
                </div>

                <div className="flex shrink-0 flex-col gap-1 opacity-0 transition-opacity group-hover:opacity-100">
                  {!item.isRead && (
                    <button
                      onClick={() => markRead.mutate(item.id)}
                      title="Đánh dấu đã đọc"
                      className="rounded p-1 text-slate-400 hover:bg-slate-100 hover:text-emerald-600"
                    >
                      <CheckCheck className="h-3.5 w-3.5" />
                    </button>
                  )}
                  <button
                    onClick={() => remove.mutate(item.id)}
                    title="Xóa"
                    className="rounded p-1 text-slate-400 hover:bg-slate-100 hover:text-rose-600"
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                  </button>
                </div>
              </div>
            ))}
          </div>

          <Link
            to="/notifications"
            onClick={() => setOpen(false)}
            className="block border-t border-slate-100 py-2.5 text-center text-xs font-medium text-brand-600 hover:bg-slate-50"
          >
            Xem tất cả thông báo
          </Link>
        </div>
      )}
    </div>
  )
}
