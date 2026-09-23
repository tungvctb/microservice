import { useQuery } from '@tanstack/react-query'
import { orderApi } from './order-api'
import { paymentApi } from '@/features/payments/payment-api'
import { Modal } from '@/components/ui/Modal'
import {
  Badge, orderStatusLabel, orderStatusTone, paymentStatusLabel, paymentStatusTone,
} from '@/components/ui/Badge'
import { formatDateTime, formatMoney } from '@/lib/format'

/** Các bước của saga, hiển thị dạng timeline để thấy đơn đang ở đâu. */
const sagaSteps = [
  { key: 'placed', label: 'Tạo đơn & giữ kho' },
  { key: 'payment', label: 'Thanh toán' },
  { key: 'confirmed', label: 'Xác nhận' },
  { key: 'shipped', label: 'Giao hàng' },
  { key: 'completed', label: 'Hoàn tất' },
]

function currentStep(status: string): number {
  switch (status) {
    case 'Pending': return 0
    case 'StockReserved':
    case 'AwaitingPayment': return 1
    case 'Paid':
    case 'Confirmed': return 2
    case 'Shipped': return 3
    case 'Completed': return 4
    default: return -1   // Cancelled / Failed
  }
}

export function OrderDetailModal({ orderId, onClose }: { orderId: string | null; onClose: () => void }) {
  const orderQuery = useQuery({
    queryKey: ['orders', orderId],
    queryFn: () => orderApi.getById(orderId!),
    enabled: orderId !== null,
    refetchInterval: (query) => {
      // Còn đang chạy saga thì tiếp tục poll; xong rồi thì dừng.
      const status = query.state.data?.status
      return status && ['Pending', 'StockReserved', 'AwaitingPayment'].includes(status) ? 3000 : false
    },
  })

  const paymentQuery = useQuery({
    queryKey: ['payments', 'order', orderId],
    queryFn: () => paymentApi.getByOrder(orderId!),
    enabled: orderId !== null,
    retry: false,   // đơn chưa thanh toán thì 404 là bình thường
  })

  const order = orderQuery.data
  const step = order ? currentStep(order.status) : -1
  const isFailed = order?.status === 'Cancelled' || order?.status === 'Failed'

  return (
    <Modal
      open={orderId !== null}
      title={order ? `Đơn ${order.orderNumber}` : 'Chi tiết đơn hàng'}
      onClose={onClose}
      size="lg"
    >
      {orderQuery.isLoading && <p className="py-10 text-center text-sm text-slate-400">Đang tải...</p>}

      {order && (
        <div className="space-y-5">
          {/* Timeline saga */}
          <section>
            <div className="mb-3 flex items-center gap-2">
              <Badge tone={orderStatusTone[order.status] ?? 'gray'}>
                {orderStatusLabel[order.status] ?? order.status}
              </Badge>
              <span className="text-xs text-slate-400">{formatDateTime(order.createdAtUtc)}</span>
            </div>

            {isFailed ? (
              <div className="rounded-lg border border-rose-200 bg-rose-50 p-3">
                <p className="text-sm font-medium text-rose-800">Saga đã bồi hoàn</p>
                <p className="mt-0.5 text-xs text-rose-600">
                  {order.cancellationReason ?? 'Đơn hàng bị hủy.'}
                </p>
              </div>
            ) : (
              <ol className="flex flex-wrap gap-1">
                {sagaSteps.map((s, index) => (
                  <li key={s.key} className="flex flex-1 flex-col gap-1.5">
                    <div
                      className={
                        index <= step ? 'h-1 rounded-full bg-brand-500' : 'h-1 rounded-full bg-slate-200'
                      }
                    />
                    <span
                      className={
                        index <= step ? 'text-[11px] font-medium text-brand-700' : 'text-[11px] text-slate-400'
                      }
                    >
                      {s.label}
                    </span>
                  </li>
                ))}
              </ol>
            )}
          </section>

          {/* Dòng hàng */}
          <section>
            <h3 className="mb-2 text-sm font-semibold text-slate-800">Sản phẩm</h3>
            <div className="overflow-hidden rounded-lg border border-slate-200">
              <table className="w-full text-sm">
                <thead className="bg-slate-50 text-left text-slate-600">
                  <tr>
                    <th className="px-3 py-2 font-medium">Sản phẩm</th>
                    <th className="px-3 py-2 text-center font-medium">SL</th>
                    <th className="px-3 py-2 text-right font-medium">Đơn giá</th>
                    <th className="px-3 py-2 text-right font-medium">Thành tiền</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {order.lines.map((line) => (
                    <tr key={line.productId}>
                      <td className="px-3 py-2">
                        <p className="text-slate-800">{line.productName}</p>
                        <p className="text-xs text-slate-400">{line.sku}</p>
                      </td>
                      <td className="px-3 py-2 text-center">{line.quantity}</td>
                      <td className="px-3 py-2 text-right">{formatMoney(line.unitPrice, line.currency)}</td>
                      <td className="px-3 py-2 text-right font-medium">
                        {formatMoney(line.lineTotal, line.currency)}
                      </td>
                    </tr>
                  ))}
                </tbody>
                <tfoot className="bg-slate-50">
                  <tr>
                    <td colSpan={3} className="px-3 py-2 text-right font-medium text-slate-600">
                      Tổng cộng
                    </td>
                    <td className="px-3 py-2 text-right text-base font-semibold text-slate-900">
                      {formatMoney(order.totalAmount, order.currency)}
                    </td>
                  </tr>
                </tfoot>
              </table>
            </div>
          </section>

          <div className="grid gap-5 sm:grid-cols-2">
            <section>
              <h3 className="mb-2 text-sm font-semibold text-slate-800">Giao hàng</h3>
              <div className="space-y-0.5 rounded-lg bg-slate-50 p-3 text-sm">
                <p className="font-medium text-slate-800">{order.shippingAddress.recipientName}</p>
                <p className="text-slate-600">{order.shippingAddress.phone}</p>
                <p className="text-slate-600">
                  {[order.shippingAddress.street, order.shippingAddress.ward,
                    order.shippingAddress.district, order.shippingAddress.city]
                    .filter(Boolean).join(', ')}
                </p>
                {order.shippingAddress.note && (
                  <p className="pt-1 text-xs italic text-slate-500">{order.shippingAddress.note}</p>
                )}
              </div>
            </section>

            <section>
              <h3 className="mb-2 text-sm font-semibold text-slate-800">Thanh toán</h3>
              <div className="space-y-1.5 rounded-lg bg-slate-50 p-3 text-sm">
                <div className="flex justify-between">
                  <span className="text-slate-500">Phương thức</span>
                  <span className="text-slate-800">{order.paymentMethod}</span>
                </div>

                {paymentQuery.data ? (
                  <>
                    <div className="flex items-center justify-between">
                      <span className="text-slate-500">Trạng thái</span>
                      <Badge tone={paymentStatusTone[paymentQuery.data.status] ?? 'gray'}>
                        {paymentStatusLabel[paymentQuery.data.status] ?? paymentQuery.data.status}
                      </Badge>
                    </div>

                    {paymentQuery.data.transactionRef && (
                      <div className="flex justify-between gap-2">
                        <span className="shrink-0 text-slate-500">Mã GD</span>
                        <span className="truncate font-mono text-xs text-slate-700">
                          {paymentQuery.data.transactionRef}
                        </span>
                      </div>
                    )}

                    {paymentQuery.data.failureReason && (
                      <p className="pt-1 text-xs text-rose-600">{paymentQuery.data.failureReason}</p>
                    )}
                  </>
                ) : (
                  <p className="text-xs text-slate-400">Chưa có giao dịch thanh toán.</p>
                )}
              </div>
            </section>
          </div>

          {/* Truy vết nội bộ — hữu ích khi soi saga */}
          <section className="rounded-lg border border-slate-200 p-3">
            <h3 className="mb-2 text-xs font-semibold uppercase tracking-wide text-slate-500">
              Tham chiếu nội bộ
            </h3>
            <dl className="grid gap-1.5 text-xs sm:grid-cols-2">
              <div className="flex gap-2">
                <dt className="shrink-0 text-slate-400">Order ID</dt>
                <dd className="truncate font-mono text-slate-600">{order.id}</dd>
              </div>
              <div className="flex gap-2">
                <dt className="shrink-0 text-slate-400">Reservation</dt>
                <dd className="truncate font-mono text-slate-600">{order.reservationId ?? '—'}</dd>
              </div>
              <div className="flex gap-2">
                <dt className="shrink-0 text-slate-400">Payment ID</dt>
                <dd className="truncate font-mono text-slate-600">{order.paymentId ?? '—'}</dd>
              </div>
              <div className="flex gap-2">
                <dt className="shrink-0 text-slate-400">Xác nhận lúc</dt>
                <dd className="truncate text-slate-600">
                  {order.confirmedAtUtc ? formatDateTime(order.confirmedAtUtc) : '—'}
                </dd>
              </div>
            </dl>
          </section>
        </div>
      )}
    </Modal>
  )
}
