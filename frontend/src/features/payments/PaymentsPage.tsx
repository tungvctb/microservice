import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { HandCoins, Undo2 } from 'lucide-react'
import { paymentApi } from './payment-api'
import { PageHeader } from '@/components/ui/PageHeader'
import { Button } from '@/components/ui/Button'
import { Badge, paymentStatusLabel, paymentStatusTone } from '@/components/ui/Badge'
import { Table, type Column } from '@/components/ui/Table'
import { Pagination } from '@/components/ui/Pagination'
import { Modal } from '@/components/ui/Modal'
import { Input } from '@/components/ui/Field'
import { ErrorState } from '@/components/ui/ErrorState'
import { useToast } from '@/components/ui/Toast'
import { formatDateTime, formatMoney } from '@/lib/format'
import { ApiException } from '@/lib/api-client'
import type { Payment } from '@/lib/types'

const statuses = ['Pending', 'Authorized', 'Captured', 'Failed', 'Refunded', 'PartiallyRefunded']

export function PaymentsPage() {
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [refunding, setRefunding] = useState<Payment | null>(null)

  const queryClient = useQueryClient()
  const toast = useToast()

  const query = { status: status || undefined, page, pageSize }

  const paymentsQuery = useQuery({
    queryKey: ['payments', query],
    queryFn: () => paymentApi.search(query),
    placeholderData: (previous) => previous,
    refetchInterval: 20_000,
  })

  const refundForm = useForm<{ amount: number; reason: string }>()

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['payments'] })

  const capture = useMutation({
    mutationFn: paymentApi.capture,
    onSuccess: () => { invalidate(); toast.success('Đã thu tiền giao dịch') },
    onError: (e) => toast.error('Thu tiền thất bại', e instanceof ApiException ? e.message : undefined),
  })

  const refund = useMutation({
    mutationFn: (values: { amount: number; reason: string }) =>
      paymentApi.refund(refunding!.id, Number(values.amount), values.reason),
    onSuccess: () => {
      invalidate()
      toast.success('Đã hoàn tiền')
      setRefunding(null)
      refundForm.reset()
    },
    onError: (e) => toast.error('Hoàn tiền thất bại', e instanceof ApiException ? e.message : undefined),
  })

  const columns: Column<Payment>[] = [
    {
      key: 'transaction',
      header: 'Giao dịch',
      render: (row) => (
        <div>
          <p className="font-mono text-xs font-medium text-slate-900">
            {row.transactionRef ?? row.id.slice(0, 8)}
          </p>
          <p className="text-xs text-slate-400">Đơn: {row.orderId.slice(0, 8)}…</p>
        </div>
      ),
    },
    {
      key: 'amount',
      header: 'Số tiền',
      align: 'right',
      render: (row) => (
        <div>
          <p className="font-medium">{formatMoney(row.amount, row.currency)}</p>
          {row.refundedAmount > 0 && (
            <p className="text-xs text-violet-600">
              Đã hoàn {formatMoney(row.refundedAmount, row.currency)}
            </p>
          )}
        </div>
      ),
    },
    { key: 'method', header: 'Phương thức', render: (row) => <span className="text-slate-600">{row.method}</span> },
    {
      key: 'status',
      header: 'Trạng thái',
      align: 'center',
      render: (row) => (
        <div>
          <Badge tone={paymentStatusTone[row.status] ?? 'gray'}>
            {paymentStatusLabel[row.status] ?? row.status}
          </Badge>
          {row.failureReason && (
            <p className="mt-1 max-w-[16rem] truncate text-xs text-rose-500" title={row.failureReason}>
              {row.failureReason}
            </p>
          )}
        </div>
      ),
    },
    {
      key: 'created',
      header: 'Thời điểm',
      render: (row) => <span className="text-slate-500">{formatDateTime(row.createdAtUtc)}</span>,
    },
    {
      key: 'actions',
      header: '',
      align: 'right',
      render: (row) => (
        <div className="flex justify-end gap-1">
          {row.status === 'Authorized' && (
            <button
              onClick={() => capture.mutate(row.id)}
              title="Thu tiền"
              className="rounded p-1.5 text-slate-400 hover:bg-slate-100 hover:text-emerald-600"
            >
              <HandCoins className="h-4 w-4" />
            </button>
          )}

          {row.refundableAmount > 0 && (
            <button
              onClick={() => {
                setRefunding(row)
                refundForm.setValue('amount', row.refundableAmount)
              }}
              title="Hoàn tiền"
              className="rounded p-1.5 text-slate-400 hover:bg-slate-100 hover:text-violet-600"
            >
              <Undo2 className="h-4 w-4" />
            </button>
          )}
        </div>
      ),
    },
  ]

  return (
    <>
      <PageHeader
        title="Thanh toán"
        description="Giao dịch được tạo tự động khi payment-service nhận sự kiện OrderPlaced từ Kafka."
      />

      <div className="card">
        <div className="flex flex-wrap gap-3 border-b border-slate-200 p-4">
          <select
            className="input w-auto"
            value={status}
            onChange={(e) => { setStatus(e.target.value); setPage(1) }}
          >
            <option value="">Mọi trạng thái</option>
            {statuses.map((s) => (
              <option key={s} value={s}>{paymentStatusLabel[s]}</option>
            ))}
          </select>
        </div>

        {paymentsQuery.isError ? (
          <ErrorState error={paymentsQuery.error} onRetry={() => paymentsQuery.refetch()} />
        ) : (
          <>
            <Table
              columns={columns}
              rows={paymentsQuery.data?.items ?? []}
              rowKey={(row) => row.id}
              loading={paymentsQuery.isLoading}
              emptyMessage="Chưa có giao dịch thanh toán nào."
            />

            {paymentsQuery.data && (
              <Pagination
                page={paymentsQuery.data.page}
                pageSize={paymentsQuery.data.pageSize}
                totalCount={paymentsQuery.data.totalCount}
                totalPages={paymentsQuery.data.totalPages}
                onPageChange={setPage}
                onPageSizeChange={(size) => { setPageSize(size); setPage(1) }}
              />
            )}
          </>
        )}
      </div>

      <Modal
        open={refunding !== null}
        title="Hoàn tiền giao dịch"
        description={
          refunding
            ? `Tối đa có thể hoàn: ${formatMoney(refunding.refundableAmount, refunding.currency)}`
            : undefined
        }
        onClose={() => setRefunding(null)}
        size="sm"
        footer={
          <>
            <Button variant="outline" onClick={() => setRefunding(null)}>Hủy</Button>
            <Button onClick={refundForm.handleSubmit((v) => refund.mutate(v))} loading={refund.isPending}>
              Hoàn tiền
            </Button>
          </>
        }
      >
        <form className="space-y-4" onSubmit={refundForm.handleSubmit((v) => refund.mutate(v))}>
          <Input
            label="Số tiền hoàn"
            type="number"
            min={1}
            max={refunding?.refundableAmount}
            {...refundForm.register('amount', { required: true, min: 1 })}
          />
          <Input
            label="Lý do"
            placeholder="Khách trả hàng, giao sai sản phẩm..."
            {...refundForm.register('reason', { required: true })}
          />
        </form>
      </Modal>
    </>
  )
}
