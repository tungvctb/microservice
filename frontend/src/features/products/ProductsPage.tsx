import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Pencil, Plus, Power, Search, Trash2 } from 'lucide-react'
import { productApi, categoryApi, type ProductQuery } from './product-api'
import { ProductFormModal } from './ProductFormModal'
import { PageHeader } from '@/components/ui/PageHeader'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Table, type Column } from '@/components/ui/Table'
import { Pagination } from '@/components/ui/Pagination'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { ErrorState } from '@/components/ui/ErrorState'
import { useToast } from '@/components/ui/Toast'
import { useAuth } from '@/features/auth/auth-context'
import { useDebounce } from '@/hooks/useDebounce'
import { formatMoney } from '@/lib/format'
import { ApiException } from '@/lib/api-client'
import type { ProductSummary } from '@/lib/types'

export function ProductsPage() {
  const { hasRole } = useAuth()
  const canManage = hasRole('Admin', 'Manager')
  const canDelete = hasRole('Admin')

  const [search, setSearch] = useState('')
  const [categoryId, setCategoryId] = useState('')
  const [activeFilter, setActiveFilter] = useState('')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(20)

  const [formOpen, setFormOpen] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [deletingId, setDeletingId] = useState<string | null>(null)

  const debouncedSearch = useDebounce(search)
  const queryClient = useQueryClient()
  const toast = useToast()

  const query: ProductQuery = {
    search: debouncedSearch || undefined,
    categoryId: categoryId || undefined,
    isActive: activeFilter === '' ? undefined : activeFilter === 'true',
    page,
    pageSize,
  }

  const productsQuery = useQuery({
    queryKey: ['products', query],
    queryFn: () => productApi.search(query),
    placeholderData: (previous) => previous,   // tránh nháy trắng khi đổi trang
  })

  const categoriesQuery = useQuery({ queryKey: ['categories'], queryFn: () => categoryApi.list() })

  const toggleStatus = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => productApi.setStatus(id, isActive),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products'] })
      toast.success('Đã đổi trạng thái sản phẩm')
    },
    onError: (e) => toast.error('Không đổi được trạng thái', e instanceof ApiException ? e.message : undefined),
  })

  const remove = useMutation({
    mutationFn: productApi.remove,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products'] })
      toast.success('Đã xóa sản phẩm')
      setDeletingId(null)
    },
    onError: (e) => toast.error('Xóa thất bại', e instanceof ApiException ? e.message : undefined),
  })

  const columns: Column<ProductSummary>[] = [
    {
      key: 'product',
      header: 'Sản phẩm',
      render: (row) => (
        <div className="flex items-center gap-3">
          {row.imageUrl ? (
            <img src={row.imageUrl} alt="" className="h-10 w-10 rounded-lg object-cover" loading="lazy" />
          ) : (
            <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-slate-100 text-xs text-slate-400">
              N/A
            </div>
          )}
          <div className="min-w-0">
            <p className="truncate font-medium text-slate-900">{row.name}</p>
            <p className="text-xs text-slate-400">{row.sku}</p>
          </div>
        </div>
      ),
    },
    {
      key: 'category',
      header: 'Danh mục',
      render: (row) => <span className="text-slate-600">{row.categoryName ?? '—'}</span>,
    },
    {
      key: 'price',
      header: 'Giá',
      align: 'right',
      render: (row) => <span className="font-medium">{formatMoney(row.price, row.currency)}</span>,
    },
    {
      key: 'status',
      header: 'Trạng thái',
      align: 'center',
      render: (row) =>
        row.isActive ? <Badge tone="green">Đang bán</Badge> : <Badge tone="gray">Ngừng bán</Badge>,
    },
    {
      key: 'actions',
      header: '',
      align: 'right',
      render: (row) => (
        <div className="flex justify-end gap-1">
          {canManage && (
            <>
              <button
                onClick={() => { setEditingId(row.id); setFormOpen(true) }}
                title="Sửa"
                className="rounded p-1.5 text-slate-400 hover:bg-slate-100 hover:text-brand-600"
              >
                <Pencil className="h-4 w-4" />
              </button>
              <button
                onClick={() => toggleStatus.mutate({ id: row.id, isActive: !row.isActive })}
                title={row.isActive ? 'Ngừng bán' : 'Mở bán'}
                className="rounded p-1.5 text-slate-400 hover:bg-slate-100 hover:text-amber-600"
              >
                <Power className="h-4 w-4" />
              </button>
            </>
          )}
          {canDelete && (
            <button
              onClick={() => setDeletingId(row.id)}
              title="Xóa"
              className="rounded p-1.5 text-slate-400 hover:bg-slate-100 hover:text-rose-600"
            >
              <Trash2 className="h-4 w-4" />
            </button>
          )}
        </div>
      ),
    },
  ]

  return (
    <>
      <PageHeader
        title="Sản phẩm"
        description="Catalog do product-service quản lý. Tồn kho nằm ở inventory-service."
        actions={
          canManage && (
            <Button icon={<Plus className="h-4 w-4" />} onClick={() => { setEditingId(null); setFormOpen(true) }}>
              Thêm sản phẩm
            </Button>
          )
        }
      />

      <div className="card">
        <div className="flex flex-wrap gap-3 border-b border-slate-200 p-4">
          <div className="relative min-w-[14rem] flex-1">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
            <input
              className="input pl-9"
              placeholder="Tìm theo tên hoặc SKU..."
              value={search}
              onChange={(e) => { setSearch(e.target.value); setPage(1) }}
            />
          </div>

          <select
            className="input w-auto"
            value={categoryId}
            onChange={(e) => { setCategoryId(e.target.value); setPage(1) }}
          >
            <option value="">Tất cả danh mục</option>
            {categoriesQuery.data?.map((c) => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </select>

          <select
            className="input w-auto"
            value={activeFilter}
            onChange={(e) => { setActiveFilter(e.target.value); setPage(1) }}
          >
            <option value="">Mọi trạng thái</option>
            <option value="true">Đang bán</option>
            <option value="false">Ngừng bán</option>
          </select>
        </div>

        {productsQuery.isError ? (
          <ErrorState error={productsQuery.error} onRetry={() => productsQuery.refetch()} />
        ) : (
          <>
            <Table
              columns={columns}
              rows={productsQuery.data?.items ?? []}
              rowKey={(row) => row.id}
              loading={productsQuery.isLoading}
              emptyMessage="Chưa có sản phẩm nào khớp bộ lọc."
            />

            {productsQuery.data && (
              <Pagination
                page={productsQuery.data.page}
                pageSize={productsQuery.data.pageSize}
                totalCount={productsQuery.data.totalCount}
                totalPages={productsQuery.data.totalPages}
                onPageChange={setPage}
                onPageSizeChange={(size) => { setPageSize(size); setPage(1) }}
              />
            )}
          </>
        )}
      </div>

      <ProductFormModal open={formOpen} productId={editingId} onClose={() => setFormOpen(false)} />

      <ConfirmDialog
        open={deletingId !== null}
        title="Xóa sản phẩm"
        message="Sản phẩm sẽ bị xóa khỏi catalog và sự kiện xóa được phát tới các service khác. Thao tác này không hoàn tác được."
        confirmLabel="Xóa"
        loading={remove.isPending}
        onConfirm={() => deletingId && remove.mutate(deletingId)}
        onCancel={() => setDeletingId(null)}
      />
    </>
  )
}
