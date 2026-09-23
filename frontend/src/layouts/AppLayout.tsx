import {
  Bell, Boxes, CreditCard, LayoutDashboard, LogOut, Menu, Package,
  ShoppingCart, Tags, Users, X,
} from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import clsx from 'clsx'
import { useAuth } from '@/features/auth/auth-context'
import { NotificationBell } from '@/features/notifications/NotificationBell'

interface NavItem {
  to: string
  label: string
  icon: ReactNode
  roles?: string[]
}

const navItems: NavItem[] = [
  { to: '/', label: 'Tổng quan', icon: <LayoutDashboard className="h-4.5 w-4.5" /> },
  { to: '/products', label: 'Sản phẩm', icon: <Package className="h-4.5 w-4.5" /> },
  { to: '/categories', label: 'Danh mục', icon: <Tags className="h-4.5 w-4.5" /> },
  { to: '/inventory', label: 'Tồn kho', icon: <Boxes className="h-4.5 w-4.5" /> },
  { to: '/orders', label: 'Đơn hàng', icon: <ShoppingCart className="h-4.5 w-4.5" /> },
  { to: '/payments', label: 'Thanh toán', icon: <CreditCard className="h-4.5 w-4.5" />, roles: ['Admin', 'Manager'] },
  { to: '/notifications', label: 'Thông báo', icon: <Bell className="h-4.5 w-4.5" /> },
  { to: '/users', label: 'Người dùng', icon: <Users className="h-4.5 w-4.5" />, roles: ['Admin'] },
]

export function AppLayout() {
  const { user, hasRole, logout } = useAuth()
  const navigate = useNavigate()
  const [sidebarOpen, setSidebarOpen] = useState(false)

  const visibleItems = navItems.filter((item) => !item.roles || hasRole(...item.roles))

  const handleLogout = async () => {
    await logout()
    navigate('/login')
  }

  return (
    <div className="min-h-screen bg-slate-50">
      {/* Sidebar */}
      <aside
        className={clsx(
          'fixed inset-y-0 left-0 z-40 w-60 border-r border-slate-200 bg-white',
          'transition-transform lg:translate-x-0',
          sidebarOpen ? 'translate-x-0' : '-translate-x-full',
        )}
      >
        <div className="flex h-14 items-center justify-between border-b border-slate-200 px-4">
          <span className="flex items-center gap-2 font-semibold text-slate-900">
            <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-brand-600 text-sm text-white">
              E
            </span>
            E-Commerce
          </span>
          <button className="lg:hidden" onClick={() => setSidebarOpen(false)} aria-label="Đóng menu">
            <X className="h-5 w-5 text-slate-400" />
          </button>
        </div>

        <nav className="space-y-0.5 p-3">
          {visibleItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === '/'}
              onClick={() => setSidebarOpen(false)}
              className={({ isActive }) =>
                clsx(
                  'flex items-center gap-2.5 rounded-lg px-3 py-2 text-sm font-medium transition-colors',
                  isActive
                    ? 'bg-brand-50 text-brand-700'
                    : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900',
                )
              }
            >
              {item.icon}
              {item.label}
            </NavLink>
          ))}
        </nav>
      </aside>

      {sidebarOpen && (
        <div className="fixed inset-0 z-30 bg-slate-900/30 lg:hidden" onClick={() => setSidebarOpen(false)} />
      )}

      {/* Nội dung */}
      <div className="lg:pl-60">
        <header className="sticky top-0 z-20 flex h-14 items-center justify-between border-b border-slate-200 bg-white/90 px-4 backdrop-blur">
          <button className="lg:hidden" onClick={() => setSidebarOpen(true)} aria-label="Mở menu">
            <Menu className="h-5 w-5 text-slate-600" />
          </button>

          <div className="ml-auto flex items-center gap-2">
            <NotificationBell />

            <div className="hidden items-center gap-2.5 border-l border-slate-200 pl-3 sm:flex">
              <div className="text-right">
                <p className="text-sm font-medium leading-tight text-slate-800">{user?.fullName}</p>
                <p className="text-xs text-slate-400">{user?.roles.join(', ')}</p>
              </div>
              <div className="flex h-8 w-8 items-center justify-center rounded-full bg-brand-100 text-sm font-semibold text-brand-700">
                {user?.fullName?.charAt(0).toUpperCase()}
              </div>
            </div>

            <button
              onClick={handleLogout}
              title="Đăng xuất"
              className="rounded-lg p-2 text-slate-500 hover:bg-slate-100 hover:text-rose-600"
            >
              <LogOut className="h-5 w-5" />
            </button>
          </div>
        </header>

        <main className="p-4 sm:p-6">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
