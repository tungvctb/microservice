import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Pencil, Plus, Trash2 } from 'lucide-react'
import { categoryApi } from '@/features/products/product-api'
import { PageHeader } from '@/components/ui/PageHeader'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import { Table, type Column } from '@/components/ui/Table'
import { Modal } from '@/components/ui/Modal'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { Input, Textarea } from '@/components/ui/Field'
import { useToast } from '@/components/ui/Toast'
import { useAuth } from '@/features/auth/auth-context'
import { formatDateTime } from '@/lib/format'
import { ApiException } from '@/lib/api-client'
import type { Category } from '@/lib/types'

const schema = z.object({
  name: z.string().min(1, 'Nhập tên danh mục').max(120),
  description: z.string().max(500).optional(),
  isActive: z.boolean(),
})

type FormValues = z.infer<typeof schema>

export function CategoriesPage() {
  const { hasRole } = useAuth()
  const canManage = hasRole('Admin', 'Manager')
  const canDelete = hasRole('Admin')

  const [formOpen, setFormOpen] = useState(false)
  const [editing, setEditing] = useState<Category | null>(null)
  const [deletingId, setDeletingId] = useState<string | null>(null)

  const queryClient = useQueryClient()
  const toast = useToast()

  const categoriesQuery = useQuery({ queryKey: ['categories'], queryFn: () => categoryApi.list() })

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: '', description: '', isActive: true },
  })

  useEffect(() => {
    if (!formOpen) return
    form.reset(
      editing
        ? { name: editing.name, description: editing.description ?? '', isActive: editing.isActive }
        : { name: '', description: '', isActive: true },
    )
  }, [formOpen, editing, form])

  const save = useMutation({
    mutationFn: (values: FormValues) =>
      editing
        ? categoryApi.update(editing.id, {
            name: values.name,
            description: values.description || undefined,
            isActive: values.isActive,
          })
        : categoryApi.create({ name: values.name, description: values.description || undefined }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['categories'] })
      toast.success(editing ? 'Đã cập nhật danh mục' : 'Đã tạo danh mục')
      setFormOpen(false)
    },
    onError: (e) => toast.error('Lưu thất bại', e instanceof ApiException ? e.message : undefined),
  })

  const remove = useMutation({
    mutationFn: categoryApi.remove,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['categories'] })
      toast.success('Đã xóa danh mục')
      setDeletingId(null)
    },
    onError: (e) =>
      toast.error('Không xóa được', e instanceof ApiException ? e.message : undefined),
  })

  const columns: Column<Category>[] = [
    {
      key: 'name',
      header: 'Danh mục',
      render: (row) => (
        <div>
          <p className="font-medium text-slate-900">{row.name}</p>
          <p className="text-xs text-slate-400">/{row.slug}</p>
        </div>
      ),
    },
    {
      key: 'description',
      header: 'Mô tả',
      render: (row) => <span className="text-slate-600">{row.description ?? '—'}</span>,
    },
    {
      key: 'count',
      header: 'Số sản phẩm',
      align: 'center',
      render: (row) => <Badge tone={row.productCount > 0 ? 'blue' : 'gray'}>{row.productCount}</Badge>,
    },
    {
      key: 'status',
      header: 'Trạng thái',
      align: 'center',
      render: (row) => (row.isActive ? <Badge tone="green">Hoạt động</Badge> : <Badge tone="gray">Tạm ẩn</Badge>),
    },
    {
      key: 'created',
      header: 'Ngày tạo',
      render: (row) => <span className="text-slate-500">{formatDateTime(row.createdAtUtc)}</span>,
    },
    {
      key: 'actions',
      header: '',
      align: 'right',
      render: (row) => (
        <div className="flex justify-end gap-1">
          {canManage && (
            <button
              onClick={() => { setEditing(row); setFormOpen(true) }}
              title="Sửa"
              className="rounded p-1.5 text-slate-400 hover:bg-slate-100 hover:text-brand-600"
            >
              <Pencil className="h-4 w-4" />
            </button>
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
        title="Danh mục"
        description="Danh mục không xóa được khi vẫn còn sản phẩm bên trong."
        actions={
          canManage && (
            <Button icon={<Plus className="h-4 w-4" />} onClick={() => { setEditing(null); setFormOpen(true) }}>
              Thêm danh mục
            </Button>
          )
        }
      />

      <div className="card">
        <Table
          columns={columns}
          rows={categoriesQuery.data ?? []}
          rowKey={(row) => row.id}
          loading={categoriesQuery.isLoading}
          emptyMessage="Chưa có danh mục nào."
        />
      </div>

      <Modal
        open={formOpen}
        title={editing ? 'Sửa danh mục' : 'Thêm danh mục'}
        onClose={() => setFormOpen(false)}
        size="sm"
        footer={
          <>
            <Button variant="outline" onClick={() => setFormOpen(false)}>Hủy</Button>
            <Button onClick={form.handleSubmit((v) => save.mutate(v))} loading={save.isPending}>
              {editing ? 'Lưu' : 'Tạo'}
            </Button>
          </>
        }
      >
        <form className="space-y-4" onSubmit={form.handleSubmit((v) => save.mutate(v))}>
          <Input label="Tên danh mục" error={form.formState.errors.name?.message} {...form.register('name')} />
          <Textarea label="Mô tả" rows={3} error={form.formState.errors.description?.message} {...form.register('description')} />

          {editing && (
            <label className="flex items-center gap-2 text-sm text-slate-700">
              <input type="checkbox" className="rounded border-slate-300" {...form.register('isActive')} />
              Đang hoạt động
            </label>
          )}
        </form>
      </Modal>

      <ConfirmDialog
        open={deletingId !== null}
        title="Xóa danh mục"
        message="Danh mục sẽ bị xóa vĩnh viễn. Nếu còn sản phẩm bên trong, hệ thống sẽ từ chối."
        confirmLabel="Xóa"
        loading={remove.isPending}
        onConfirm={() => deletingId && remove.mutate(deletingId)}
        onCancel={() => setDeletingId(null)}
      />
    </>
  )
}
