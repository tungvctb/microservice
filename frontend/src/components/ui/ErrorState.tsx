import { ServerCrash } from 'lucide-react'
import { Button } from './Button'

export function ErrorState({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  const message = error instanceof Error ? error.message : 'Đã có lỗi xảy ra.'

  return (
    <div className="flex flex-col items-center justify-center gap-3 px-4 py-14 text-center">
      <ServerCrash className="h-10 w-10 text-slate-300" />
      <div>
        <p className="font-medium text-slate-700">Không tải được dữ liệu</p>
        <p className="mt-1 max-w-md text-sm text-slate-500">{message}</p>
      </div>
      {onRetry && (
        <Button variant="outline" size="sm" onClick={onRetry}>
          Thử lại
        </Button>
      )}
    </div>
  )
}
