import { CheckCircle2, Info, TriangleAlert, XCircle, X } from 'lucide-react'
import {
  createContext, useCallback, useContext, useMemo, useState, type ReactNode,
} from 'react'
import clsx from 'clsx'

type ToastTone = 'success' | 'error' | 'info' | 'warning'

interface Toast {
  id: string
  tone: ToastTone
  title: string
  message?: string
}

interface ToastApi {
  success: (title: string, message?: string) => void
  error: (title: string, message?: string) => void
  info: (title: string, message?: string) => void
  warning: (title: string, message?: string) => void
}

const ToastContext = createContext<ToastApi | null>(null)

const icons: Record<ToastTone, ReactNode> = {
  success: <CheckCircle2 className="h-5 w-5 text-emerald-600" />,
  error: <XCircle className="h-5 w-5 text-rose-600" />,
  info: <Info className="h-5 w-5 text-blue-600" />,
  warning: <TriangleAlert className="h-5 w-5 text-amber-600" />,
}

const borders: Record<ToastTone, string> = {
  success: 'border-l-emerald-500',
  error: 'border-l-rose-500',
  info: 'border-l-blue-500',
  warning: 'border-l-amber-500',
}

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([])

  const dismiss = useCallback((id: string) => {
    setToasts((current) => current.filter((t) => t.id !== id))
  }, [])

  const push = useCallback(
    (tone: ToastTone, title: string, message?: string) => {
      const id = crypto.randomUUID()
      setToasts((current) => [...current, { id, tone, title, message }])
      // Lỗi để lâu hơn vì người dùng cần đọc kỹ.
      setTimeout(() => dismiss(id), tone === 'error' ? 7000 : 4000)
    },
    [dismiss],
  )

  const api = useMemo<ToastApi>(
    () => ({
      success: (title, message) => push('success', title, message),
      error: (title, message) => push('error', title, message),
      info: (title, message) => push('info', title, message),
      warning: (title, message) => push('warning', title, message),
    }),
    [push],
  )

  return (
    <ToastContext.Provider value={api}>
      {children}

      <div className="pointer-events-none fixed bottom-4 right-4 z-[60] flex w-full max-w-sm flex-col gap-2">
        {toasts.map((toast) => (
          <div
            key={toast.id}
            className={clsx(
              'pointer-events-auto flex gap-3 rounded-lg border border-l-4 border-slate-200',
              'bg-white p-3 shadow-lg animate-in slide-in-from-right',
              borders[toast.tone],
            )}
          >
            <div className="shrink-0 pt-0.5">{icons[toast.tone]}</div>
            <div className="min-w-0 flex-1">
              <p className="text-sm font-medium text-slate-900">{toast.title}</p>
              {toast.message && <p className="mt-0.5 break-words text-xs text-slate-500">{toast.message}</p>}
            </div>
            <button
              onClick={() => dismiss(toast.id)}
              className="shrink-0 self-start rounded p-0.5 text-slate-400 hover:text-slate-600"
              aria-label="Đóng thông báo"
            >
              <X className="h-4 w-4" />
            </button>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  )
}

export function useToast(): ToastApi {
  const context = useContext(ToastContext)
  if (!context) throw new Error('useToast phải nằm trong <ToastProvider>.')
  return context
}
