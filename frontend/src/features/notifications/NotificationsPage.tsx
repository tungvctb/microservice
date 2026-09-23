import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { CheckCheck, Circle, Send, Trash2 } from 'lucide-react'
import { Link } from 'react-router-dom'
import clsx from 'clsx'
import { notificationApi } from './notification-api'
import { PageHeader } from '@/components/ui/PageHeader'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Pagination } from '@/components/ui/Pagination'
import { Modal } from '@/components/ui/Modal'
import { Input, Select, Textarea } from '@/components/ui/Field'
import { ErrorState } from '@/components/ui/ErrorState'
import { useToast } from '@/components/ui/Toast'
import { useAuth } from '@/features/auth/auth-context'
import { formatDateTime, formatRelative } from '@/lib/format'
import { ApiException } from '@/lib/api-client'
import type { NotificationSeverity } from '@/lib/types'

const severityTone: Record<NotificationSeverity, 'blue' | 'green' | 'amber' | 'red'> = {
  Info: 'blue', Success: 'green', Warning: 'amber', Error: 'red',
}

const severityLabel: Record<NotificationSeverity, string> = {
  Info: 'Thông tin', Success: 'Thành công', Warning: 'Cảnh báo', Error: 'Lỗi',
}

export function NotificationsPage() {
  const { hasRole } = useAuth()
  const canBroadcast = hasRole('Admin', 'Manager')

  const [readFilter, setReadFilter] = useState('')
  const [page, setPage] = useState(1)
  const [composeOpen, setComposeOpen] = useState(false)

  const queryClient = useQueryClient()
  const toast = useToast()

  const query = {
    isRead: readFilter === '' ? undefined : readFilter === 'true',
    page,
    pageSize: 20,
  }

  const listQuery = useQuery({
    queryKey: ['notifications', query],
    queryFn: () => notificationApi.list(query),
    placeholderData: (previous) => previous,
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['notifications'] })

  const markRead = useMutation({ mutationFn: notificationApi.markRead, onSuccess: invalidate })
  const markAllRead = useMutation({
    mutationFn: notificationApi.markAllRead,
    onSuccess: () => { invalidate(); toast.success('Đã đánh dấu tất cả là đã đọc') },
  })
  const remove = useMutation({ mutationFn: notificationApi.remove, onSuccess: invalidate })

  const composeForm = useForm<{
    title: string; body: string; severity: NotificationSeverity; targetRole: string; link?: string
  }>({ defaultValues: { severity: 'Info', targetRole: '' } })

  const broadcast = useMutation({
    mutationFn: (values: { title: string; body: string; severity: string; targetRole: string; link?: string }) =>
      notificationApi.create({
        recipientId: null,
        title: values.title,
        body: values.body,
        severity: values.severity,
        link: values.link || undefined,
        targetRole: values.targetRole || undefined,
      }),
    onSuccess: () => {
      invalidate()
      toast.success('Đã gửi thông báo', 'Thông báo được đẩy realtime qua SignalR.')
      setComposeOpen(false)
      composeForm.reset({ severity: 'Info', targetRole: '' })
    },
    onError: (e) => toast.error('Gửi thất bại', e instanceof ApiException ? e.message : undefined),
  })

  return (
    <>
      <PageHeader
        title="Thông báo"
        description="Notification-service nghe mọi topic Kafka và đẩy realtime qua SignalR (backplane Redis)."
        actions={
          <div className="flex gap-2">
            {canBroadcast && (
              <Button variant="outline" icon={<Send className="h-4 w-4" />} onClick={() => setComposeOpen(true)}>
                Gửi thông báo
              </Button>
            )}
            <Button
              variant="secondary"
              icon={<CheckCheck className="h-4 w-4" />}
              onClick={() => markAllRead.mutate()}
              loading={markAllRead.isPending}
            >
              Đọc tất cả
            </Button>
          </div>
        }
      />

      <div className="card">
        <div className="flex flex-wrap gap-3 border-b border-slate-200 p-4">
          <select
            className="input w-auto"
            value={readFilter}
            onChange={(e) => { setReadFilter(e.target.value); setPage(1) }}
          >
            <option value="">Tất cả</option>
            <option value="false">Chưa đọc</option>
            <option value="true">Đã đọc</option>
          </select>
        </div>

        {listQuery.isError ? (
          <ErrorState error={listQuery.error} onRetry={() => listQuery.refetch()} />
        ) : (
          <>
            <div className="divide-y divide-slate-100">
              {listQuery.isLoading && (
                <p className="py-12 text-center text-sm text-slate-400">Đang tải...</p>
              )}

              {listQuery.data?.items.length === 0 && (
                <p className="py-12 text-center text-sm text-slate-400">Chưa có thông báo nào.</p>
              )}

              {listQuery.data?.items.map((item) => (
                <article
                  key={item.id}
                  className={clsx('group flex gap-3 p-4', !item.isRead && 'bg-brand-50/40')}
                >
                  <Circle
                    className={clsx(
                      'mt-1.5 h-2.5 w-2.5 shrink-0 fill-current',
                      item.severity === 'Error' ? 'text-rose-500'
                        : item.severity === 'Warning' ? 'text-amber-500'
                        : item.severity === 'Success' ? 'text-emerald-500' : 'text-blue-500',
                    )}
                  />

                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <h3 className="font-medium text-slate-900">{item.title}</h3>
                      <Badge tone={severityTone[item.severity]}>{severityLabel[item.severity]}</Badge>
                      {item.recipientId === null && <Badge tone="violet">Chung</Badge>}
                    </div>

                    <p className="mt-1 text-sm text-slate-600">{item.body}</p>

                    <div className="mt-1.5 flex flex-wrap items-center gap-3 text-xs text-slate-400">
                      <span title={formatDateTime(item.createdAtUtc)}>{formatRelative(item.createdAtUtc)}</span>
                      {item.link && (
                        <Link
                          to={item.link}
                          onClick={() => !item.isRead && markRead.mutate(item.id)}
                          className="font-medium text-brand-600 hover:text-brand-700"
                        >
                          Xem chi tiết →
                        </Link>
                      )}
                      {item.metadata?.event && (
                        <code className="rounded bg-slate-100 px-1.5 py-0.5 text-[11px] text-slate-500">
                          {item.metadata.event}
                        </code>
                      )}
                    </div>
                  </div>

                  <div className="flex shrink-0 gap-1 opacity-0 transition-opacity group-hover:opacity-100">
                    {!item.isRead && (
                      <button
                        onClick={() => markRead.mutate(item.id)}
                        title="Đánh dấu đã đọc"
                        className="rounded p-1.5 text-slate-400 hover:bg-slate-100 hover:text-emerald-600"
                      >
                        <CheckCheck className="h-4 w-4" />
                      </button>
                    )}
                    <button
                      onClick={() => remove.mutate(item.id)}
                      title="Xóa"
                      className="rounded p-1.5 text-slate-400 hover:bg-slate-100 hover:text-rose-600"
                    >
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </div>
                </article>
              ))}
            </div>

            {listQuery.data && (
              <Pagination
                page={listQuery.data.page}
                pageSize={listQuery.data.pageSize}
                totalCount={listQuery.data.totalCount}
                totalPages={listQuery.data.totalPages}
                onPageChange={setPage}
              />
            )}
          </>
        )}
      </div>

      <Modal
        open={composeOpen}
        title="Gửi thông báo"
        description="Để trống vai trò nếu muốn gửi cho tất cả người dùng."
        onClose={() => setComposeOpen(false)}
        size="sm"
        footer={
          <>
            <Button variant="outline" onClick={() => setComposeOpen(false)}>Hủy</Button>
            <Button onClick={composeForm.handleSubmit((v) => broadcast.mutate(v))} loading={broadcast.isPending}>
              Gửi ngay
            </Button>
          </>
        }
      >
        <form className="space-y-4" onSubmit={composeForm.handleSubmit((v) => broadcast.mutate(v))}>
          <Input label="Tiêu đề" {...composeForm.register('title', { required: true })} />
          <Textarea label="Nội dung" rows={3} {...composeForm.register('body', { required: true })} />

          <Select label="Mức độ" {...composeForm.register('severity')}>
            <option value="Info">Thông tin</option>
            <option value="Success">Thành công</option>
            <option value="Warning">Cảnh báo</option>
            <option value="Error">Lỗi</option>
          </Select>

          <Select label="Gửi tới vai trò" {...composeForm.register('targetRole')}>
            <option value="">Tất cả người dùng</option>
            <option value="Admin">Admin</option>
            <option value="Manager">Manager</option>
            <option value="Customer">Customer</option>
          </Select>

          <Input label="Đường dẫn kèm theo" placeholder="/orders" {...composeForm.register('link')} />
        </form>
      </Modal>
    </>
  )
}
