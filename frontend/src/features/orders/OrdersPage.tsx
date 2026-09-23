import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Ban, CheckCircle2, Eye, Plus, Search, Truck } from 'lucide-react'
import { orderApi } from './order-api'
import { PlaceOrderModal } from './PlaceOrderModal'
import { OrderDetailModal } from './OrderDetailModal'
import { PageHeader } from '@/components/ui/PageHeader'
import { Button } from '@/components/ui/Button'
import { Badge, orderStatusLabel, orderStatusTone } from '@/components/ui/Badge'
import { Table, type Column } from '@/components/ui/Table'
import { Pagination } from '@/components/ui/Pagination'
import { Modal } from '@/components/ui/Modal'
import { Input } from '@/components/ui/Field'
import { ErrorState } from '@/components/ui/ErrorState'
import { useToast } from '@/components/ui/Toast'
import { useAuth } from '@/features/auth/auth-context'
import { useDebounce } from '@/hooks/useDebounce'
import { formatDateTime, formatMoney } from '@/lib/format'
import { ApiException } from '@/lib/api-client'
import type { OrderSummary } from '@/lib/types'

const statuses = [
  'Pending', 'StockReserved', 'AwaitingPayment', 'Paid',
  'Confirmed', 'Shipped', 'Completed', 'Cancelled', 'Failed',
]

const cancellableStatuses = ['Pending', 'StockReserved', 'AwaitingPayment', 'Paid', 'Confirmed']

export function OrdersPage() {
  const { hasRole } = useAuth()
  const isStaff = hasRole('Admin', 'Manager')

  const [orderNumber, setOrderNumber] = useState('')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(20)

  const [placing, setPlacing] = useState(false)
  const [detailId, setDetailId] = useState<string | null>(null)
  const [cancelling, setCancelling] = useState<OrderSummary | null>(null)
  const [cancelReason, setCancelReason] = useState('')

  const debouncedNumber = useDebounce(orderNumber)
  const queryClient = useQueryClient()
  const toast = useToast()

  const query = {
    orderNumber: debouncedNumber || undefined,
    status: status || undefined,
    page,
    pageSize,
  }

  const ordersQuery = useQuery({
    queryKey: ['orders', query],
    queryFn: () => orderApi.search(query),
    placeholderData: (previous) => previous,
    // Saga chạy bất đồng bộ; poll để thấy trạng thái nhảy dù SignalR có trục trặc.
    refetchInterval: 15_000,
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['orders'] })

  const cancel = useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) => orderApi.cancel(id, reason),
    onSuccess: () => {
      invalidate()
      queryClient.invalidateQueries({ queryKey: ['stocks'] })
      toast.success('Đã hủy đơn', 'Kho sẽ được nhả tự động qua sự kiện Kafka.')
      setCancelling(null)
      setCancelReason('')
    },
    onError: (e) => toast.error('Hủy đơn thất bại', e instanceof ApiException ? e.message : undefined),
  })

  const ship = useMutation({
    mutationFn: orderApi.ship,
    onSuccess: () => { invalidate(); toast.success('Đã chuyển sang trạng thái đang giao') },
    onError: (e) => toast.error('Thao tác thất bại', e instanceof ApiException ? e.message : undefined),
  })

  const complete = useMutation({
    mutationFn: orderApi.complete,
    onSuccess: () => { invalidate(); toast.success('Đơn hàng đã hoàn tất') },
    onError: (e) => toast.error('Thao tác thất bại', e instanceof ApiException ? e.message : undefined),
  })

  const columns: Column<OrderSummary>[] = [
    {
      key: 'number',
      header: 'Mã đơn',
      render: (row) => <span className="font-mono text-xs font-medium text-slate-900">{row.orderNumber}</span>,
    },
    {
      key: 'status',
      header: 'Trạng thái',
      align: 'center',
      render: (row) => (
        <Badge tone={orderStatusTone[row.status] ?? 'gray'}>
          {orderStatusLabel[row.status] ?? row.status}
        </Badge>
      ),
    },
    { key: 'items', header: 'Số món', align: 'center', render: (row) => row.itemCount },
    {
      key: 'total',
      header: 'Tổng tiền',
      align: 'right',
      render: (row) => <span className="font-medium">{formatMoney(row.totalAmount, row.currency)}</span>,
    },
    {
      key: 'created',
      header: 'Ngày đặt',
      render: (row) => <span className="text-slate-500">{formatDateTime(row.createdAtUtc)}</span>,
    },
    {
      key: 'actions',
      header: '',
      align: 'right',
      render: (row) => (
        <div className="flex justify-end gap-1">
          <button
            onClick={() => setDetailId(row.id)}
            title="Xem chi tiết"
            className="rounded p-1.5 text-slate-400 hover:bg-slate-100 hover:text-brand-600"
          >
            <Eye className="h-4 w-4" />
          </button>

          {isStaff && row.status === 'Confirmed' && (
            <button
              onClick={() => ship.mutate(row.id)}
              title="Chuyển sang đang giao"
              className="rounded p-1.5 text-slate-400 hover:bg-slate-100 hover:text-violet-600"
            >
              <Truck className="h-4 w-4" />
            </button>
          )}

          {isStaff && row.status === 'Shipped' && (
            <button
              onClick={() => complete.mutate(row.id)}
              title="Hoàn tất đơn"
              className="rounded p-1.5 text-slate-400 hover:bg-slate-100 hover:text-emerald-600"
            >
              <CheckCircle2 className="h-4 w-4" />
            </button>
          )}

          {cancellableStatuses.includes(row.status) && (
            <button
              onClick={() => setCancelling(row)}
              title="Hủy đơn"
              className="rounded p-1.5 text-slate-400 hover:bg-slate-100 hover:text-rose-600"
            >
              <Ban className="h-4 w-4" />
            </button>
          )}
        </div>
      ),
    },
  ]

  return (
    <>
      <PageHeader
        title="Đơn hàng"
        description="Mỗi đơn kích hoạt một saga: định giá → giữ kho → thanh toán → xác nhận (hoặc bồi hoàn)."
        actions={
          <Button icon={<Plus className="h-4 w-4" />} onClick={() => setPlacing(true)}>
            Tạo đơn hàng
          </Button>
        }
      />

      <div className="card">
        <div className="flex flex-wrap gap-3 border-b border-slate-200 p-4">
          <div className="relative min-w-[14rem] flex-1">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
            <input
              className="input pl-9"
              placeholder="Tìm theo mã đơn..."
              value={orderNumber}
              onChange={(e) => { setOrderNumber(e.target.value); setPage(1) }}
            />
          </div>

          <select
            className="input w-auto"
            value={status}
            onChange={(e) => { setStatus(e.target.value); setPage(1) }}
          >
            <option value="">Mọi trạng thái</option>
            {statuses.map((s) => (
              <option key={s} value={s}>{orderStatusLabel[s]}</option>
            ))}
          </select>
        </div>

        {ordersQuery.isError ? (
          <ErrorState error={ordersQuery.error} onRetry={() => ordersQuery.refetch()} />
        ) : (
          <>
            <Table
              columns={columns}
              rows={ordersQuery.data?.items ?? []}
              rowKey={(row) => row.id}
              loading={ordersQuery.isLoading}
              emptyMessage="Chưa có đơn hàng nào."
              onRowClick={(row) => setDetailId(row.id)}
            />

            {ordersQuery.data && (
              <Pagination
                page={ordersQuery.data.page}
                pageSize={ordersQuery.data.pageSize}
                totalCount={ordersQuery.data.totalCount}
                totalPages={ordersQuery.data.totalPages}
                onPageChange={setPage}
                onPageSizeChange={(size) => { setPageSize(size); setPage(1) }}
              />
            )}
          </>
        )}
      </div>

      <PlaceOrderModal open={placing} onClose={() => setPlacing(false)} />
      <OrderDetailModal orderId={detailId} onClose={() => setDetailId(null)} />

      <Modal
        open={cancelling !== null}
        title={`Hủy đơn ${cancelling?.orderNumber ?? ''}`}
        description="Sự kiện hủy sẽ được phát qua Kafka để Inventory nhả hàng đang giữ chỗ."
        onClose={() => setCancelling(null)}
        size="sm"
        footer={
          <>
            <Button variant="outline" onClick={() => setCancelling(null)}>Đóng</Button>
            <Button
              variant="danger"
              loading={cancel.isPending}
              disabled={cancelReason.trim().length === 0}
              onClick={() => cancelling && cancel.mutate({ id: cancelling.id, reason: cancelReason })}
            >
              Xác nhận hủy
            </Button>
          </>
        }
      >
        <Input
          label="Lý do hủy"
          placeholder="Khách đổi ý, sai thông tin giao hàng..."
          value={cancelReason}
          onChange={(e) => setCancelReason(e.target.value)}
        />
      </Modal>
    </>
  )
}
