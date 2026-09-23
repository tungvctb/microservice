import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Search, ShieldCheck, UserCog } from 'lucide-react'
import { api } from '@/lib/api-client'
import { PageHeader } from '@/components/ui/PageHeader'
import { Badge } from '@/components/ui/Badge'
import { Button } from '@/components/ui/Button'
import { Table, type Column } from '@/components/ui/Table'
import { Pagination } from '@/components/ui/Pagination'
import { Modal } from '@/components/ui/Modal'
import { ErrorState } from '@/components/ui/ErrorState'
import { useToast } from '@/components/ui/Toast'
import { useDebounce } from '@/hooks/useDebounce'
import { formatDateTime } from '@/lib/format'
import { ApiException } from '@/lib/api-client'
import type { PagedResult, User } from '@/lib/types'

const allRoles = ['Admin', 'Manager', 'Customer']

const userApi = {
  search: (params: { search?: string; role?: string; isActive?: boolean; page: number; pageSize: number }) =>
    api.get<PagedResult<User>>('/users', { params }),
  setRoles: (id: string, roles: string[]) => api.put<User>(`/users/${id}/roles`, { roles }),
  setActive: (id: string, isActive: boolean) => api.patch<User>(`/users/${id}/status`, { isActive }),
}

export function UsersPage() {
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [editingRoles, setEditingRoles] = useState<User | null>(null)
  const [selectedRoles, setSelectedRoles] = useState<string[]>([])

  const debouncedSearch = useDebounce(search)
  const queryClient = useQueryClient()
  const toast = useToast()

  const query = { search: debouncedSearch || undefined, page, pageSize }

  const usersQuery = useQuery({
    queryKey: ['users', query],
    queryFn: () => userApi.search(query),
    placeholderData: (previous) => previous,
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['users'] })

  const setRoles = useMutation({
    mutationFn: ({ id, roles }: { id: string; roles: string[] }) => userApi.setRoles(id, roles),
    onSuccess: () => {
      invalidate()
      toast.success('Đã cập nhật vai trò')
      setEditingRoles(null)
    },
    onError: (e) => toast.error('Cập nhật thất bại', e instanceof ApiException ? e.message : undefined),
  })

  const setActive = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => userApi.setActive(id, isActive),
    onSuccess: () => {
      invalidate()
      toast.success('Đã đổi trạng thái tài khoản', 'Mọi phiên đăng nhập của tài khoản này đã bị thu hồi.')
    },
    onError: (e) => toast.error('Thao tác thất bại', e instanceof ApiException ? e.message : undefined),
  })

  const columns: Column<User>[] = [
    {
      key: 'user',
      header: 'Người dùng',
      render: (row) => (
        <div className="flex items-center gap-3">
          <div className="flex h-9 w-9 items-center justify-center rounded-full bg-brand-100 text-sm font-semibold text-brand-700">
            {row.fullName.charAt(0).toUpperCase()}
          </div>
          <div className="min-w-0">
            <p className="truncate font-medium text-slate-900">{row.fullName}</p>
            <p className="truncate text-xs text-slate-400">{row.email}</p>
          </div>
        </div>
      ),
    },
    { key: 'phone', header: 'Điện thoại', render: (row) => <span className="text-slate-600">{row.phone ?? '—'}</span> },
    {
      key: 'roles',
      header: 'Vai trò',
      render: (row) => (
        <div className="flex flex-wrap gap-1">
          {row.roles.map((role) => (
            <Badge key={role} tone={role === 'Admin' ? 'violet' : role === 'Manager' ? 'blue' : 'gray'}>
              {role}
            </Badge>
          ))}
        </div>
      ),
    },
    {
      key: 'status',
      header: 'Trạng thái',
      align: 'center',
      render: (row) => (row.isActive ? <Badge tone="green">Hoạt động</Badge> : <Badge tone="red">Bị khóa</Badge>),
    },
    {
      key: 'lastLogin',
      header: 'Đăng nhập gần nhất',
      render: (row) => (
        <span className="text-slate-500">
          {row.lastLoginAtUtc ? formatDateTime(row.lastLoginAtUtc) : 'Chưa đăng nhập'}
        </span>
      ),
    },
    {
      key: 'actions',
      header: '',
      align: 'right',
      render: (row) => (
        <div className="flex justify-end gap-1">
          <button
            onClick={() => { setEditingRoles(row); setSelectedRoles(row.roles) }}
            title="Đổi vai trò"
            className="rounded p-1.5 text-slate-400 hover:bg-slate-100 hover:text-brand-600"
          >
            <UserCog className="h-4 w-4" />
          </button>
          <button
            onClick={() => setActive.mutate({ id: row.id, isActive: !row.isActive })}
            title={row.isActive ? 'Khóa tài khoản' : 'Mở khóa'}
            className="rounded p-1.5 text-slate-400 hover:bg-slate-100 hover:text-amber-600"
          >
            <ShieldCheck className="h-4 w-4" />
          </button>
        </div>
      ),
    },
  ]

  const toggleRole = (role: string) => {
    setSelectedRoles((current) =>
      current.includes(role) ? current.filter((r) => r !== role) : [...current, role],
    )
  }

  return (
    <>
      <PageHeader
        title="Người dùng"
        description="Khóa tài khoản sẽ thu hồi toàn bộ refresh token của người đó ngay lập tức."
      />

      <div className="card">
        <div className="border-b border-slate-200 p-4">
          <div className="relative max-w-sm">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
            <input
              className="input pl-9"
              placeholder="Tìm theo tên hoặc email..."
              value={search}
              onChange={(e) => { setSearch(e.target.value); setPage(1) }}
            />
          </div>
        </div>

        {usersQuery.isError ? (
          <ErrorState error={usersQuery.error} onRetry={() => usersQuery.refetch()} />
        ) : (
          <>
            <Table
              columns={columns}
              rows={usersQuery.data?.items ?? []}
              rowKey={(row) => row.id}
              loading={usersQuery.isLoading}
              emptyMessage="Không tìm thấy người dùng."
            />

            {usersQuery.data && (
              <Pagination
                page={usersQuery.data.page}
                pageSize={usersQuery.data.pageSize}
                totalCount={usersQuery.data.totalCount}
                totalPages={usersQuery.data.totalPages}
                onPageChange={setPage}
                onPageSizeChange={(size) => { setPageSize(size); setPage(1) }}
              />
            )}
          </>
        )}
      </div>

      <Modal
        open={editingRoles !== null}
        title={`Vai trò của ${editingRoles?.fullName ?? ''}`}
        description="Phải chọn ít nhất 1 vai trò."
        onClose={() => setEditingRoles(null)}
        size="sm"
        footer={
          <>
            <Button variant="outline" onClick={() => setEditingRoles(null)}>Hủy</Button>
            <Button
              disabled={selectedRoles.length === 0}
              loading={setRoles.isPending}
              onClick={() => editingRoles && setRoles.mutate({ id: editingRoles.id, roles: selectedRoles })}
            >
              Lưu
            </Button>
          </>
        }
      >
        <div className="space-y-2">
          {allRoles.map((role) => (
            <label
              key={role}
              className="flex cursor-pointer items-center gap-2.5 rounded-lg border border-slate-200 p-3 hover:bg-slate-50"
            >
              <input
                type="checkbox"
                className="rounded border-slate-300"
                checked={selectedRoles.includes(role)}
                onChange={() => toggleRole(role)}
              />
              <div>
                <p className="text-sm font-medium text-slate-800">{role}</p>
                <p className="text-xs text-slate-400">
                  {role === 'Admin' ? 'Toàn quyền, kể cả xóa dữ liệu'
                    : role === 'Manager' ? 'Quản lý sản phẩm, kho và đơn hàng'
                    : 'Chỉ đặt hàng và xem đơn của mình'}
                </p>
              </div>
            </label>
          ))}
        </div>
      </Modal>
    </>
  )
}
