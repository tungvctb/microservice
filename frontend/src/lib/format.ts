import { formatDistanceToNow, format } from 'date-fns'
import { vi } from 'date-fns/locale'

export const formatMoney = (amount: number, currency = 'VND') =>
  currency === 'VND'
    ? `${new Intl.NumberFormat('vi-VN').format(amount)} ₫`
    : new Intl.NumberFormat('vi-VN', { style: 'currency', currency }).format(amount)

export const formatNumber = (value: number) => new Intl.NumberFormat('vi-VN').format(value)

export const formatDateTime = (iso: string) => {
  const date = new Date(iso.endsWith('Z') ? iso : `${iso}Z`)
  return format(date, 'dd/MM/yyyy HH:mm', { locale: vi })
}

export const formatRelative = (iso: string) => {
  const date = new Date(iso.endsWith('Z') ? iso : `${iso}Z`)
  return formatDistanceToNow(date, { addSuffix: true, locale: vi })
}
