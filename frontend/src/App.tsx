import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider } from '@/features/auth/auth-context'
import { ProtectedRoute } from '@/features/auth/ProtectedRoute'
import { LoginPage } from '@/features/auth/LoginPage'
import { RegisterPage } from '@/features/auth/RegisterPage'
import { AppLayout } from '@/layouts/AppLayout'
import { DashboardPage } from '@/features/dashboard/DashboardPage'
import { ProductsPage } from '@/features/products/ProductsPage'
import { CategoriesPage } from '@/features/categories/CategoriesPage'
import { InventoryPage } from '@/features/inventory/InventoryPage'
import { OrdersPage } from '@/features/orders/OrdersPage'
import { PaymentsPage } from '@/features/payments/PaymentsPage'
import { NotificationsPage } from '@/features/notifications/NotificationsPage'
import { UsersPage } from '@/features/users/UsersPage'
import { ToastProvider } from '@/components/ui/Toast'
import { ApiException } from '@/lib/api-client'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: false,
      retry: (failureCount, error) => {
        // Lỗi nghiệp vụ (4xx) retry cũng vô ích — chỉ thử lại với lỗi mạng/5xx.
        if (error instanceof ApiException && error.status && error.status < 500) return false
        return failureCount < 2
      },
    },
  },
})

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <ToastProvider>
        <BrowserRouter>
          <AuthProvider>
            <Routes>
              <Route path="/login" element={<LoginPage />} />
              <Route path="/register" element={<RegisterPage />} />

              <Route element={<ProtectedRoute />}>
                <Route element={<AppLayout />}>
                  <Route index element={<DashboardPage />} />
                  <Route path="products" element={<ProductsPage />} />
                  <Route path="categories" element={<CategoriesPage />} />
                  <Route path="inventory" element={<InventoryPage />} />
                  <Route path="orders" element={<OrdersPage />} />
                  <Route path="notifications" element={<NotificationsPage />} />

                  <Route element={<ProtectedRoute roles={['Admin', 'Manager']} />}>
                    <Route path="payments" element={<PaymentsPage />} />
                  </Route>

                  <Route element={<ProtectedRoute roles={['Admin']} />}>
                    <Route path="users" element={<UsersPage />} />
                  </Route>
                </Route>
              </Route>

              <Route path="*" element={<Navigate to="/" replace />} />
            </Routes>
          </AuthProvider>
        </BrowserRouter>
      </ToastProvider>
    </QueryClientProvider>
  )
}
