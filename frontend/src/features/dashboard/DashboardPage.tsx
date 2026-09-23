import { useQuery } from '@tanstack/react-query'
import {
  AlertTriangle, Ban, CheckCircle2, Clock, Package, ShoppingCart, TrendingUp,
} from 'lucide-react'
import { Link } from 'react-router-dom'
import { orderApi } from '@/features/orders/order-api'
import { productApi } from '@/features/products/product-api'
import { inventoryApi } from '@/features/inventory/inventory-api'
import { PageHeader } from '@/components/ui/PageHeader'
import { StatCard } from '@/components/ui/StatCard'
import { Badge, orderStatusLabel, orderStatusTone } from '@/components/ui/Badge'
import { useAuth } from '@/features/auth/auth-context'
import { formatMoney, formatNumber, formatRelative } from '@/lib/format'

export function DashboardPage() {
  const { user, hasRole } = useAuth()
  const isStaff = hasRole('Admin', 'Manager')

  const statsQuery = useQuery({ queryKey: ['orders', 'statistics'], queryFn: orderApi.statistics })

  const recentOrdersQuery = useQuery({
    queryKey: ['orders', { page: 1, pageSize: 8 }],
    queryFn: () => orderApi.search({ page: 1, pageSize: 8 }),
    refetchInterval: 20_000,
  })

  const productsQuery = useQuery({
    queryKey: ['products', { pageSize: 1 }],
    queryFn: () => productApi.search({ pageSize: 1 }),
  })

  const lowStockQuery = useQuery({
    queryKey: ['stocks', 'low-stock'],
    queryFn: inventoryApi.lowStock,
    enabled: isStaff,
  })

  const stats = statsQuery.data

  return (
    <>
      <PageHeader
        title={`Xin chào, ${user?.fullName ?? ''}`}
        description="Tổng quan hoạt động của hệ thống microservices."
      />

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <StatCard
          label="Tổng đơn hàng"
          value={formatNumber(stats?.totalOrders ?? 0)}
          icon={<ShoppingCart className="h-5 w-5" />}
          tone="blue"
          hint={isStaff ? 'Toàn hệ thống' : 'Đơn của bạn'}
        />
        <StatCard
          label="Đang chờ xử lý"
          value={formatNumber(stats?.pendingOrders ?? 0)}
          icon={<Clock className="h-5 w-5" />}
          tone="amber"
          hint="Saga chưa kết thúc"
        />
        <StatCard
          label="Đã xác nhận"
          value={formatNumber(stats?.confirmedOrders ?? 0)}
          icon={<CheckCircle2 className="h-5 w-5" />}
          tone="green"
        />
        <StatCard
          label="Doanh thu"
          value={formatMoney(stats?.totalRevenue ?? 0)}
          icon={<TrendingUp className="h-5 w-5" />}
          tone="violet"
          hint="Đơn đã thanh toán"
        />
      </div>

      <div className="mt-4 grid gap-4 lg:grid-cols-3">
        {/* Đơn gần đây */}
        <section className="card lg:col-span-2">
          <header className="flex items-center justify-between border-b border-slate-200 px-5 py-3.5">
            <h2 className="text-sm font-semibold text-slate-900">Đơn hàng gần đây</h2>
            <Link to="/orders" className="text-xs font-medium text-brand-600 hover:text-brand-700">
              Xem tất cả →
            </Link>
          </header>

          <div className="divide-y divide-slate-100">
            {recentOrdersQuery.isLoading && (
              <p className="px-5 py-10 text-center text-sm text-slate-400">Đang tải...</p>
            )}

            {recentOrdersQuery.data?.items.length === 0 && (
              <p className="px-5 py-10 text-center text-sm text-slate-400">
                Chưa có đơn hàng nào. Vào mục Đơn hàng để tạo đơn thử nghiệm.
              </p>
            )}

            {recentOrdersQuery.data?.items.map((order) => (
              <div key={order.id} className="flex items-center gap-3 px-5 py-3">
                <div className="min-w-0 flex-1">
                  <p className="font-mono text-xs font-medium text-slate-900">{order.orderNumber}</p>
                  <p className="text-xs text-slate-400">
                    {order.itemCount} món · {formatRelative(order.createdAtUtc)}
                  </p>
                </div>

                <Badge tone={orderStatusTone[order.status] ?? 'gray'}>
                  {orderStatusLabel[order.status] ?? order.status}
                </Badge>

                <span className="w-28 shrink-0 text-right text-sm font-medium text-slate-800">
                  {formatMoney(order.totalAmount, order.currency)}
                </span>
              </div>
            ))}
          </div>
        </section>

        {/* Cột phải */}
        <div className="space-y-4">
          <section className="card p-5">
            <div className="flex items-center gap-2 text-slate-900">
              <Package className="h-4.5 w-4.5 text-slate-400" />
              <h2 className="text-sm font-semibold">Catalog</h2>
            </div>
            <p className="mt-3 text-2xl font-semibold text-slate-900">
              {formatNumber(productsQuery.data?.totalCount ?? 0)}
            </p>
            <p className="text-xs text-slate-400">sản phẩm trong hệ thống</p>
            <Link
              to="/products"
              className="mt-3 inline-block text-xs font-medium text-brand-600 hover:text-brand-700"
            >
              Quản lý sản phẩm →
            </Link>
          </section>

          {isStaff && (
            <section className="card">
              <header className="flex items-center justify-between border-b border-slate-200 px-5 py-3.5">
                <div className="flex items-center gap-2">
                  <AlertTriangle className="h-4 w-4 text-amber-500" />
                  <h2 className="text-sm font-semibold text-slate-900">Hàng sắp hết</h2>
                </div>
                {(lowStockQuery.data?.length ?? 0) > 0 && (
                  <Badge tone="amber">{lowStockQuery.data!.length}</Badge>
                )}
              </header>

              <div className="max-h-64 divide-y divide-slate-100 overflow-y-auto">
                {lowStockQuery.data?.length === 0 && (
                  <p className="px-5 py-8 text-center text-sm text-slate-400">
                    Mọi mặt hàng đều đủ tồn kho.
                  </p>
                )}

                {lowStockQuery.data?.map((stock) => (
                  <div key={stock.id} className="flex items-center justify-between px-5 py-2.5">
                    <div className="min-w-0">
                      <p className="truncate text-sm font-medium text-slate-800">{stock.sku}</p>
                      <p className="text-xs text-slate-400">ngưỡng {stock.reorderLevel}</p>
                    </div>
                    <span className="shrink-0 text-sm font-semibold text-rose-600">
                      {stock.quantityAvailable}
                    </span>
                  </div>
                ))}
              </div>

              <Link
                to="/inventory"
                className="block border-t border-slate-100 py-2.5 text-center text-xs font-medium text-brand-600 hover:bg-slate-50"
              >
                Đi tới quản lý kho
              </Link>
            </section>
          )}

          {(stats?.cancelledOrders ?? 0) > 0 && (
            <section className="card p-5">
              <div className="flex items-center gap-2">
                <Ban className="h-4.5 w-4.5 text-rose-400" />
                <h2 className="text-sm font-semibold text-slate-900">Đơn bị hủy</h2>
              </div>
              <p className="mt-2 text-2xl font-semibold text-rose-600">
                {formatNumber(stats!.cancelledOrders)}
              </p>
              <p className="mt-1 text-xs text-slate-400">
                Phần lớn do thanh toán thất bại — kho đã được nhả tự động.
              </p>
            </section>
          )}
        </div>
      </div>
    </>
  )
}
