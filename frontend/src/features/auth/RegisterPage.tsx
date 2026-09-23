import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Link, Navigate, useNavigate } from 'react-router-dom'
import { UserPlus } from 'lucide-react'
import { useAuth } from './auth-context'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Field'
import { ApiException } from '@/lib/api-client'

const schema = z
  .object({
    fullName: z.string().min(1, 'Nhập họ tên').max(120),
    email: z.string().min(1, 'Nhập email').email('Email không hợp lệ'),
    phone: z.string().regex(/^[0-9+\-\s]{8,15}$/, 'Số điện thoại không hợp lệ').optional().or(z.literal('')),
    // Quy tắc phải khớp validator phía server, nếu không người dùng sẽ bị từ chối sau khi submit.
    password: z
      .string()
      .min(8, 'Tối thiểu 8 ký tự')
      .regex(/[A-Z]/, 'Cần ít nhất 1 chữ hoa')
      .regex(/[a-z]/, 'Cần ít nhất 1 chữ thường')
      .regex(/[0-9]/, 'Cần ít nhất 1 chữ số'),
    confirmPassword: z.string(),
  })
  .refine((data) => data.password === data.confirmPassword, {
    message: 'Mật khẩu nhập lại không khớp',
    path: ['confirmPassword'],
  })

type FormValues = z.infer<typeof schema>

export function RegisterPage() {
  const { register: registerUser, isAuthenticated, loading } = useAuth()
  const navigate = useNavigate()
  const [serverError, setServerError] = useState<string | null>(null)

  const form = useForm<FormValues>({ resolver: zodResolver(schema) })

  if (!loading && isAuthenticated) return <Navigate to="/" replace />

  const onSubmit = async (values: FormValues) => {
    setServerError(null)
    try {
      await registerUser({
        email: values.email,
        password: values.password,
        fullName: values.fullName,
        phone: values.phone || undefined,
      })
      navigate('/', { replace: true })
    } catch (error) {
      setServerError(error instanceof ApiException ? error.message : 'Đăng ký thất bại.')
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-slate-100 to-brand-50 p-4">
      <div className="w-full max-w-md">
        <h1 className="mb-6 text-center text-xl font-semibold text-slate-900">Tạo tài khoản mới</h1>

        <form onSubmit={form.handleSubmit(onSubmit)} className="card space-y-4 p-6">
          {serverError && (
            <div className="rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-700">
              {serverError}
            </div>
          )}

          <Input label="Họ và tên" error={form.formState.errors.fullName?.message} {...form.register('fullName')} />
          <Input label="Email" type="email" error={form.formState.errors.email?.message} {...form.register('email')} />
          <Input label="Số điện thoại" hint="Không bắt buộc" error={form.formState.errors.phone?.message} {...form.register('phone')} />
          <Input
            label="Mật khẩu"
            type="password"
            hint="Tối thiểu 8 ký tự, có chữ hoa, chữ thường và số"
            error={form.formState.errors.password?.message}
            {...form.register('password')}
          />
          <Input
            label="Nhập lại mật khẩu"
            type="password"
            error={form.formState.errors.confirmPassword?.message}
            {...form.register('confirmPassword')}
          />

          <Button type="submit" className="w-full" loading={form.formState.isSubmitting} icon={<UserPlus className="h-4 w-4" />}>
            Đăng ký
          </Button>

          <p className="text-center text-sm text-slate-500">
            Đã có tài khoản?{' '}
            <Link to="/login" className="font-medium text-brand-600 hover:text-brand-700">
              Đăng nhập
            </Link>
          </p>
        </form>
      </div>
    </div>
  )
}
