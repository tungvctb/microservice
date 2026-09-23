import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { AlertTriangle, History, PackagePlus, Search, SlidersHorizontal } from 'lucide-react'
import { inventoryApi } from './inventory-api'
import { PageHeader } from '@/components/ui/PageHeader'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Table, type Column } from '@/components/ui/Table'
import { Pagination } from '@/components/ui/Pagination'
import { Modal } from '@/components/ui/Modal'
import { Input } from '@/components/ui/Field'
import { useToast } from '@/components/ui/Toast'
import { ErrorState } from '@/components/ui/ErrorState'
import { useAuth } from '@/features/auth/auth-context'
import { useDebounce } from '@/hooks/useDebounce'
import { formatDateTime, formatNumber } from '@/lib/format'
import { ApiException } from '@/lib/api-client'
import type { StockItem, StockMovementType } from '@/lib/types'

const movementTone: Record<StockMovementType, 'green' | 'red' | 'amber' | 'blue' | 'gray'> = {
  Inbound: 'green',
  Outbound: 'red',
  Reserved: 'amber',
  Released: 'blue',
  Adjustment: 'gray',
}

const movementLabel: Record<StockMovementType, string> = {
  Inbound: 'Nhập kho',
  Outbound: 'Xuất kho',
  Reserved: 'Giữ chỗ',
  Released: 'Nhả giữ chỗ',
  Adjustment: 'Điều chỉnh',
}

export function InventoryPage() {
  const { hasRole } = useAuth()
  const canManage = hasRole('Admin', 'Manager')

  const [search, setSearch] = useState('')
  const [lowOnly, setLowOnly] = useState(false)
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(20)

  const [receiving, setReceiving] = useState<StockItem | null>(null)
  const [adjusting, setAdjusting] = useState<StockItem | null>(null)
  const [viewingHistory, setViewingHistory] = useState<StockItem | null>(null)

  const debouncedSearch = useDebounce(search)
  const queryClient = useQueryClient()
  const toast = useToast()

  const query = {
    search: debouncedSearch || undefined,
    lowStockOnly: lowOnly || undefined,
    page,
    pageSize,
  }

  const stocksQuery = useQuery({
    queryKey: ['stocks', query],
    queryFn: () => inventoryApi.search(query),
    placeholderData: (previous) => previous,
    refetchInterval: 30_000,   // tồn kho đổi liên tục do saga đặt hàng
  })

  const movementsQuery = useQuery({
    queryKey: ['stock-movements', viewingHistory?.productId],
    queryFn: () => inventoryApi.movements(viewingHistory!.productId, 1, 50),
    enabled: viewingHistory !== null,
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['stocks'] })

  const receiveForm = useForm<{ quantity: number; reference?: string; note?: string }>()
  const adjustForm = useForm<{ newQuantityOnHand: number; reason: string }>()

  const receive = useMutation({
    mutationFn: (values: { quantity: number; reference?: string; note?: string }) =>
      inventoryApi.receive(receiving!.productId, {
        quantity: Number(values.quantity),
        reference: values.reference || undefined,
        note: values.note || undefined,
      }),
    onSuccess: () => {
      invalidate()
      toast.success('Đã nhập kho')
      setReceiving(null)
      receiveForm.reset()
    },
    onError: (e) => toast.error('Nhập kho thất bại', e instanceof ApiException ? e.message : undefined),
  })

  const adjust = useMutation({
    mutationFn: (values: { newQuantityOnHand: number; reason: string }) =>
      inventoryApi.adjust(adjusting!.productId, {
        newQuantityOnHand: Number(values.newQuantityOnHand),
        reason: values.reason,
      }),
    onSuccess: () => {
      invalidate()
      toast.success('Đã điều chỉnh tồn kho')
      setAdjusting(null)
      adjustForm.reset()
    },
    onError: (e) => toast.error('Điều chỉnh thất bại', e instanceof ApiException ? e.message : undefined),
  })

  const columns: Column<StockItem>[] = [
    {
      key: 'sku',
      header: 'SKU',
      render: (row) => (
        <div className="flex items-center gap-2">
          <span className="font-medium text-slate-900">{row.sku}</span>
          {row.isLowStock && <AlertTriangle className="h-4 w-4 text-amber-500" />}
        </div>
      ),
    },
    { key: 'warehouse', header: 'Kho', render: (row) => <span className="text-slate-600">{row.warehouseCode}</span> },
    {
      key: 'onHand',
      header: 'Tồn thực',
      align: 'right',
      render: (row) => formatNumber(row.quantityOnHand),
    },
    {
      key: 'reserved',
      header: 'Đang giữ chỗ',
      align: 'right',
      render: (row) => (
        <span className={row.quantityReserved > 0 ? 'font-medium text-amber-600' : 'text-slate-400'}>
          {formatNumber(row.quantityReserved)}
        </span>
      ),
    },
    {
      key: 'available',
      header: 'Khả dụng',
      align: 'right',
      render: (row) => (
        <span className={row.isLowStock ? 'font-semibold text-rose-600' : 'font-semibold text-emerald-600'}>
          {formatNumber(row.quantityAvailable)}
        </span>
      ),
    },
    { key: 'reorder', header: 'Ngưỡng', align: 'right', render: (row) => formatNumber(row.reorderLevel) },
    {
      key: 'status',
      header: 'Tình trạng',
      align: 'center',
      render: (row) => (row.isLowStock ? <Badge tone="amber">Sắp hết</Badge> : <Badge tone="green">Đủ hàng</Badge>),
    },
    {
      key: 'actions',
      header: '',
      align: 'right',
      render: (row) => (
        <div className="flex justify-end gap-1">
          <button
            onClick={() => setViewingHistory(row)}
            title="Lịch sử biến động"
            className="rounded p-1.5 text-slate-400 hover:bg-slate-100 hover:text-slate-700"
          >
            <History className="h-4 w-4" />
          </button>
          {canManage && (
            <>
              <button
                onClick={() => setReceiving(row)}
                title="Nhập kho"
                className="rounded p-1.5 text-slate-400 hover:bg-slate-100 hover:text-emerald-600"
              >
                <PackagePlus className="h-4 w-4" />
              </button>
              <button
                onClick={() => {
                  setAdjusting(row)
                  adjustForm.setValue('newQuantityOnHand', row.quantityOnHand)
                }}
                title="Điều chỉnh"
                className="rounded p-1.5 text-slate-400 hover:bg-slate-100 hover:text-brand-600"
              >
                <SlidersHorizontal className="h-4 w-4" />
              </button>
            </>
          )}
        </div>
      ),
    },
  ]

  return (
    <>
      <PageHeader
        title="Tồn kho"
        description="Khả dụng = Tồn thực − Đang giữ chỗ. Hàng giữ chỗ thuộc về đơn đang chờ thanh toán."
      />

      <div className="card">
        <div className="flex flex-wrap items-center gap-3 border-b border-slate-200 p-4">
          <div className="relative min-w-[14rem] flex-1">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
            <input
              className="input pl-9"
              placeholder="Tìm theo SKU..."
              value={search}
              onChange={(e) => { setSearch(e.target.value); setPage(1) }}
            />
          </div>

          <label className="flex items-center gap-2 text-sm text-slate-700">
            <input
              type="checkbox"
              className="rounded border-slate-300"
              checked={lowOnly}
              onChange={(e) => { setLowOnly(e.target.checked); setPage(1) }}
            />
            Chỉ hiện hàng sắp hết
          </label>
        </div>

        {stocksQuery.isError ? (
          <ErrorState error={stocksQuery.error} onRetry={() => stocksQuery.refetch()} />
        ) : (
          <>
            <Table
              columns={columns}
              rows={stocksQuery.data?.items ?? []}
              rowKey={(row) => row.id}
              loading={stocksQuery.isLoading}
              emptyMessage="Chưa có dữ liệu tồn kho. Tạo sản phẩm mới để inventory-service tự sinh bản ghi."
            />

            {stocksQuery.data && (
              <Pagination
                page={stocksQuery.data.page}
                pageSize={stocksQuery.data.pageSize}
                totalCount={stocksQuery.data.totalCount}
                totalPages={stocksQuery.data.totalPages}
                onPageChange={setPage}
                onPageSizeChange={(size) => { setPageSize(size); setPage(1) }}
              />
            )}
          </>
        )}
      </div>

      {/* Nhập kho */}
      <Modal
        open={receiving !== null}
        title={`Nhập kho — ${receiving?.sku ?? ''}`}
        description={`Tồn hiện tại: ${formatNumber(receiving?.quantityOnHand ?? 0)}`}
        onClose={() => setReceiving(null)}
        size="sm"
        footer={
          <>
            <Button variant="outline" onClick={() => setReceiving(null)}>Hủy</Button>
            <Button onClick={receiveForm.handleSubmit((v) => receive.mutate(v))} loading={receive.isPending}>
              Nhập kho
            </Button>
          </>
        }
      >
        <form className="space-y-4" onSubmit={receiveForm.handleSubmit((v) => receive.mutate(v))}>
          <Input label="Số lượng nhập" type="number" min={1} {...receiveForm.register('quantity', { required: true, min: 1 })} />
          <Input label="Mã tham chiếu" hint="Số phiếu nhập, mã PO..." {...receiveForm.register('reference')} />
          <Input label="Ghi chú" {...receiveForm.register('note')} />
        </form>
      </Modal>

      {/* Điều chỉnh */}
      <Modal
        open={adjusting !== null}
        title={`Điều chỉnh tồn — ${adjusting?.sku ?? ''}`}
        description={`Không được đặt nhỏ hơn số đang giữ chỗ (${formatNumber(adjusting?.quantityReserved ?? 0)}).`}
        onClose={() => setAdjusting(null)}
        size="sm"
        footer={
          <>
            <Button variant="outline" onClick={() => setAdjusting(null)}>Hủy</Button>
            <Button onClick={adjustForm.handleSubmit((v) => adjust.mutate(v))} loading={adjust.isPending}>
              Cập nhật
            </Button>
          </>
        }
      >
        <form className="space-y-4" onSubmit={adjustForm.handleSubmit((v) => adjust.mutate(v))}>
          <Input label="Tồn thực sau kiểm kê" type="number" min={0} {...adjustForm.register('newQuantityOnHand', { required: true, min: 0 })} />
          <Input label="Lý do điều chỉnh" placeholder="Kiểm kê định kỳ, hàng hỏng..." {...adjustForm.register('reason', { required: true })} />
        </form>
      </Modal>

      {/* Lịch sử */}
      <Modal
        open={viewingHistory !== null}
        title={`Lịch sử biến động — ${viewingHistory?.sku ?? ''}`}
        onClose={() => setViewingHistory(null)}
        size="lg"
      >
        {movementsQuery.isLoading ? (
          <p className="py-8 text-center text-sm text-slate-400">Đang tải...</p>
        ) : movementsQuery.data?.items.length === 0 ? (
          <p className="py-8 text-center text-sm text-slate-400">Chưa có biến động nào.</p>
        ) : (
          <div className="space-y-2">
            {movementsQuery.data?.items.map((movement) => (
              <div key={movement.id} className="flex items-start gap-3 rounded-lg border border-slate-100 p-3">
                <Badge tone={movementTone[movement.type]}>{movementLabel[movement.type]}</Badge>

                <div className="min-w-0 flex-1">
                  <p className="text-sm text-slate-700">
                    <span className={movement.quantity >= 0 ? 'text-emerald-600' : 'text-rose-600'}>
                      {movement.quantity >= 0 ? '+' : ''}{formatNumber(movement.quantity)}
                    </span>
                    {' → còn '}
                    <span className="font-medium">{formatNumber(movement.quantityAfter)}</span>
                  </p>
                  {movement.note && <p className="mt-0.5 text-xs text-slate-500">{movement.note}</p>}
                  {movement.reference && (
                    <p className="mt-0.5 font-mono text-[11px] text-slate-400">Ref: {movement.reference}</p>
                  )}
                </div>

                <span className="shrink-0 text-xs text-slate-400">{formatDateTime(movement.createdAtUtc)}</span>
              </div>
            ))}
          </div>
        )}
      </Modal>
    </>
  )
}
