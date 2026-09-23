import clsx from 'clsx'
import { Inbox, Loader2 } from 'lucide-react'
import type { ReactNode } from 'react'

export interface Column<T> {
  key: string
  header: ReactNode
  render: (row: T) => ReactNode
  className?: string
  align?: 'left' | 'center' | 'right'
}

interface Props<T> {
  columns: Column<T>[]
  rows: T[]
  rowKey: (row: T) => string
  loading?: boolean
  emptyMessage?: string
  onRowClick?: (row: T) => void
}

const alignments = { left: 'text-left', center: 'text-center', right: 'text-right' }

export function Table<T>({
  columns, rows, rowKey, loading, emptyMessage = 'Chưa có dữ liệu.', onRowClick,
}: Props<T>) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-slate-200 bg-slate-50">
            {columns.map((col) => (
              <th
                key={col.key}
                className={clsx(
                  'whitespace-nowrap px-4 py-3 font-medium text-slate-600',
                  alignments[col.align ?? 'left'],
                  col.className,
                )}
              >
                {col.header}
              </th>
            ))}
          </tr>
        </thead>

        <tbody className="divide-y divide-slate-100">
          {loading && (
            <tr>
              <td colSpan={columns.length} className="px-4 py-12 text-center text-slate-400">
                <Loader2 className="mx-auto mb-2 h-6 w-6 animate-spin" />
                Đang tải dữ liệu...
              </td>
            </tr>
          )}

          {!loading && rows.length === 0 && (
            <tr>
              <td colSpan={columns.length} className="px-4 py-12 text-center text-slate-400">
                <Inbox className="mx-auto mb-2 h-8 w-8" />
                {emptyMessage}
              </td>
            </tr>
          )}

          {!loading &&
            rows.map((row) => (
              <tr
                key={rowKey(row)}
                onClick={() => onRowClick?.(row)}
                className={clsx(
                  'transition-colors hover:bg-slate-50',
                  onRowClick && 'cursor-pointer',
                )}
              >
                {columns.map((col) => (
                  <td
                    key={col.key}
                    className={clsx('px-4 py-3', alignments[col.align ?? 'left'], col.className)}
                  >
                    {col.render(row)}
                  </td>
                ))}
              </tr>
            ))}
        </tbody>
      </table>
    </div>
  )
}
