import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Link, Navigate, useNavigate, useSearchParams } from 'react-router-dom'
import { LogIn } from 'lucide-react'
import { useAuth } from './auth-context'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Field'
import { ApiException } from '@/lib/api-client'

const schema = z.object({
  email: z.string().min(1, 'Nhập email').email('Email không hợp lệ'),
  password: z.string().min(1, 'Nhập mật khẩu'),
})

type FormValues = z.infer<typeof schema>

const demoAccounts = [
  { label: 'Admin', email: 'admin@shop.local', password: 'Admin@123' },
  { label: 'Quản lý', email: 'manager@shop.local', password: 'Manager@123' },
  { label: 'Khách hàng', email: 'customer@shop.local', password: 'Customer@123' },
]

export function LoginPage() {
  const { login, isAuthenticated, loading } = useAuth()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const [serverError, setServerError] = useState<string | null>(null)

  const form = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { email: '', password: '' } })

  if (!loading && isAuthenticated) return <Navigate to={params.get('redirect') ?? '/'} replace />

  const onSubmit = async (values: FormValues) => {
    setServerError(null)
    try {
      await login(values.email, values.password)
      navigate(params.get('redirect') ?? '/', { replace: true })
    } catch (error) {
      setServerError(error instanceof ApiException ? error.message : 'Đăng nhập thất bại.')
    }
  }

  const fillDemo = (email: string, password: string) => {
    form.setValue('email', email)
    form.setValue('password', password)
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-slate-100 to-brand-50 p-4">
      <div className="w-full max-w-md">
        <div className="mb-6 text-center">
          <div className="mx-auto mb-3 flex h-12 w-12 items-center justify-center rounded-xl bg-brand-600 text-xl font-bold text-white">
            E
          </div>
          <h1 className="text-xl font-semibold text-slate-900">Đăng nhập hệ thống</h1>
          <p className="mt-1 text-sm text-slate-500">Bảng điều khiển E-Commerce Microservices</p>
        </div>

        <form onSubmit={form.handleSubmit(onSubmit)} className="card space-y-4 p-6">
          {serverError && (
            <div className="rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-700">
              {serverError}
            </div>
          )}

          <Input
            label="Email"
            type="email"
            autoComplete="email"
            placeholder="admin@shop.local"
            error={form.formState.errors.email?.message}
            {...form.register('email')}
          />

          <Input
            label="Mật khẩu"
            type="password"
            autoComplete="current-password"
            placeholder="••••••••"
            error={form.formState.errors.password?.message}
            {...form.register('password')}
          />

          <Button
            type="submit"
            className="w-full"
            loading={form.formState.isSubmitting}
            icon={<LogIn className="h-4 w-4" />}
          >
            Đăng nhập
          </Button>

          <p className="text-center text-sm text-slate-500">
            Chưa có tài khoản?{' '}
            <Link to="/register" className="font-medium text-brand-600 hover:text-brand-700">
              Đăng ký
            </Link>
          </p>
        </form>

        <div className="mt-4 rounded-lg border border-slate-200 bg-white/70 p-3">
          <p className="mb-2 text-xs font-medium text-slate-500">Tài khoản mẫu (bấm để điền nhanh)</p>
          <div className="flex flex-wrap gap-2">
            {demoAccounts.map((account) => (
              <button
                key={account.email}
                type="button"
                onClick={() => fillDemo(account.email, account.password)}
                className="rounded-md border border-slate-200 bg-white px-2.5 py-1 text-xs text-slate-600 hover:border-brand-300 hover:text-brand-600"
              >
                {account.label}
              </button>
            ))}
          </div>
        </div>
      </div>
    </div>
  )
}
