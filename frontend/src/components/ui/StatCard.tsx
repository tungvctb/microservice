import clsx from 'clsx'
import type { ReactNode } from 'react'

interface Props {
  label: string
  value: ReactNode
  icon: ReactNode
  tone?: 'blue' | 'green' | 'amber' | 'red' | 'violet'
  hint?: string
}

const tones = {
  blue: 'bg-blue-50 text-blue-600',
  green: 'bg-emerald-50 text-emerald-600',
  amber: 'bg-amber-50 text-amber-600',
  red: 'bg-rose-50 text-rose-600',
  violet: 'bg-violet-50 text-violet-600',
}

export function StatCard({ label, value, icon, tone = 'blue', hint }: Props) {
  return (
    <div className="card p-5">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-sm text-slate-500">{label}</p>
          <p className="mt-1 truncate text-2xl font-semibold text-slate-900">{value}</p>
          {hint && <p className="mt-1 text-xs text-slate-400">{hint}</p>}
        </div>
        <div className={clsx('flex h-11 w-11 shrink-0 items-center justify-center rounded-lg', tones[tone])}>
          {icon}
        </div>
      </div>
    </div>
  )
}
