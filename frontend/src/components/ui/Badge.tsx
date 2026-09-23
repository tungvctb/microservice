import clsx from 'clsx'
import type { ReactNode } from 'react'

type Tone = 'gray' | 'blue' | 'green' | 'amber' | 'red' | 'violet'

const tones: Record<Tone, string> = {
  gray: 'bg-slate-100 text-slate-700 ring-slate-200',
  blue: 'bg-blue-50 text-blue-700 ring-blue-200',
  green: 'bg-emerald-50 text-emerald-700 ring-emerald-200',
  amber: 'bg-amber-50 text-amber-700 ring-amber-200',
  red: 'bg-rose-50 text-rose-700 ring-rose-200',
  violet: 'bg-violet-50 text-violet-700 ring-violet-200',
}

export function Badge({ tone = 'gray', children }: { tone?: Tone; children: ReactNode }) {
  return (
    <span
      className={clsx(
        'inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset',
        tones[tone],
      )}
    >
      {children}
    </span>
  )
}

/** Ánh xạ trạng thái đơn hàng sang màu — dùng chung cho mọi màn hình. */
export const orderStatusTone: Record<string, Tone> = {
  Pending: 'gray',
  StockReserved: 'blue',
  AwaitingPayment: 'amber',
  Paid: 'green',
  Confirmed: 'green',
  Shipped: 'violet',
  Completed: 'green',
  Cancelled: 'red',
  Failed: 'red',
}

export const orderStatusLabel: Record<string, string> = {
  Pending: 'Chờ xử lý',
  StockReserved: 'Đã giữ hàng',
  AwaitingPayment: 'Chờ thanh toán',
  Paid: 'Đã thanh toán',
  Confirmed: 'Đã xác nhận',
  Shipped: 'Đang giao',
  Completed: 'Hoàn tất',
  Cancelled: 'Đã hủy',
  Failed: 'Thất bại',
}

export const paymentStatusTone: Record<string, Tone> = {
  Pending: 'gray',
  Authorized: 'blue',
  Captured: 'green',
  Failed: 'red',
  Refunded: 'violet',
  PartiallyRefunded: 'amber',
}

export const paymentStatusLabel: Record<string, string> = {
  Pending: 'Chờ xử lý',
  Authorized: 'Đã giữ tiền',
  Captured: 'Đã thu tiền',
  Failed: 'Thất bại',
  Refunded: 'Đã hoàn tiền',
  PartiallyRefunded: 'Hoàn một phần',
}
