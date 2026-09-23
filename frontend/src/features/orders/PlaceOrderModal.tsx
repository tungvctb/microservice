import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Minus, Plus, Trash2 } from 'lucide-react'
import { orderApi } from './order-api'
import { productApi } from '@/features/products/product-api'
import { inventoryApi } from '@/features/inventory/inventory-api'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { Input, Select } from '@/components/ui/Field'
import { useToast } from '@/components/ui/Toast'
import { formatMoney } from '@/lib/format'
import { ApiException } from '@/lib/api-client'
import type { PaymentMethodType, ProductSummary } from '@/lib/types'

interface CartLine {
  product: ProductSummary
  quantity: number
}

interface AddressForm {
  recipientName: string
  phone: string
  street: string
  ward: string
  district: string
  city: string
  note?: string
  paymentMethod: PaymentMethodType
}

const paymentMethods: { value: PaymentMethodType; label: string }[] = [
  { value: 'CreditCard', label: 'Thẻ tín dụng' },
  { value: 'BankTransfer', label: 'Chuyển khoản' },
  { value: 'Wallet', label: 'Ví điện tử' },
  { value: 'Cod', label: 'Thanh toán khi nhận hàng (COD)' },
]

export function PlaceOrderModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const [cart, setCart] = useState<CartLine[]>([])
  const [search, setSearch] = useState('')
  const queryClient = useQueryClient()
  const toast = useToast()

  const productsQuery = useQuery({
    queryKey: ['products', { search, isActive: true, pageSize: 50 }],
    queryFn: () => productApi.search({ search: search || undefined, isActive: true, pageSize: 50 }),
    enabled: open,
  })

  // Tồn kho hiển thị ngay cạnh sản phẩm để người đặt biết trước còn hàng hay không.
  const stocksQuery = useQuery({
    queryKey: ['stocks', { pageSize: 100 }],
    queryFn: () => inventoryApi.search({ pageSize: 100 }),
    enabled: open,
  })

  const stockByProduct = new Map(stocksQuery.data?.items.map((s) => [s.productId, s]) ?? [])

  const form = useForm<AddressForm>({
    defaultValues: { city: 'Hồ Chí Minh', paymentMethod: 'CreditCard' },
  })

  const total = cart.reduce((sum, line) => sum + line.product.price * line.quantity, 0)
  const currency = cart[0]?.product.currency ?? 'VND'

  const addToCart = (product: ProductSummary) => {
    setCart((current) => {
      const existing = current.find((line) => line.product.id === product.id)
      return existing
        ? current.map((line) =>
            line.product.id === product.id ? { ...line, quantity: line.quantity + 1 } : line,
          )
        : [...current, { product, quantity: 1 }]
    })
  }

  const changeQuantity = (productId: string, delta: number) => {
    setCart((current) =>
      current
        .map((line) =>
          line.product.id === productId ? { ...line, quantity: line.quantity + delta } : line,
        )
        .filter((line) => line.quantity > 0),
    )
  }

  const place = useMutation({
    mutationFn: (values: AddressForm) =>
      orderApi.place({
        lines: cart.map((line) => ({ productId: line.product.id, quantity: line.quantity })),
        shippingAddress: {
          recipientName: values.recipientName,
          phone: values.phone,
          street: values.street,
          ward: values.ward,
          district: values.district,
          city: values.city,
          note: values.note,
        },
        paymentMethod: values.paymentMethod,
      }),
    onSuccess: (order) => {
      queryClient.invalidateQueries({ queryKey: ['orders'] })
      queryClient.invalidateQueries({ queryKey: ['stocks'] })
      toast.success(
        `Đã tạo đơn ${order.orderNumber}`,
        'Hệ thống đang xử lý thanh toán — theo dõi thông báo realtime để biết kết quả.',
      )
      setCart([])
      form.reset()
      onClose()
    },
    onError: (error) => {
      toast.error('Đặt hàng thất bại', error instanceof ApiException ? error.message : undefined)
    },
  })

  return (
    <Modal
      open={open}
      title="Tạo đơn hàng"
      description="Giá được chốt lại tại catalog qua gRPC; kho được giữ chỗ trước khi thanh toán."
      onClose={onClose}
      size="lg"
      footer={
        <>
          <Button variant="outline" onClick={onClose}>Hủy</Button>
          <Button
            disabled={cart.length === 0}
            loading={place.isPending}
            onClick={form.handleSubmit((v) => place.mutate(v))}
          >
            Đặt hàng · {formatMoney(total, currency)}
          </Button>
        </>
      }
    >
      <div className="grid gap-5 lg:grid-cols-2">
        {/* Chọn sản phẩm */}
        <section>
          <h3 className="mb-2 text-sm font-semibold text-slate-800">1. Chọn sản phẩm</h3>

          <input
            className="input mb-3"
            placeholder="Tìm sản phẩm..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />

          <div className="max-h-64 space-y-1.5 overflow-y-auto pr-1">
            {productsQuery.data?.items.map((product) => {
              const stock = stockByProduct.get(product.id)
              const available = stock?.quantityAvailable ?? 0

              return (
                <button
                  key={product.id}
                  type="button"
                  onClick={() => addToCart(product)}
                  disabled={available <= 0}
                  className="flex w-full items-center gap-2 rounded-lg border border-slate-200 p-2 text-left transition-colors hover:border-brand-300 hover:bg-brand-50/40 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-medium text-slate-900">{product.name}</p>
                    <p className="text-xs text-slate-400">
                      {product.sku} · còn {available}
                    </p>
                  </div>
                  <span className="shrink-0 text-sm font-medium text-slate-700">
                    {formatMoney(product.price, product.currency)}
                  </span>
                </button>
              )
            })}

            {productsQuery.data?.items.length === 0 && (
              <p className="py-6 text-center text-sm text-slate-400">Không tìm thấy sản phẩm.</p>
            )}
          </div>

          {cart.length > 0 && (
            <div className="mt-4">
              <h4 className="mb-2 text-sm font-semibold text-slate-800">Giỏ hàng</h4>
              <div className="space-y-1.5">
                {cart.map((line) => (
                  <div key={line.product.id} className="flex items-center gap-2 rounded-lg bg-slate-50 p-2">
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm text-slate-800">{line.product.name}</p>
                      <p className="text-xs text-slate-400">
                        {formatMoney(line.product.price * line.quantity, line.product.currency)}
                      </p>
                    </div>

                    <div className="flex shrink-0 items-center gap-1">
                      <button
                        type="button"
                        onClick={() => changeQuantity(line.product.id, -1)}
                        className="rounded border border-slate-300 bg-white p-1 hover:bg-slate-100"
                      >
                        <Minus className="h-3 w-3" />
                      </button>
                      <span className="w-7 text-center text-sm font-medium">{line.quantity}</span>
                      <button
                        type="button"
                        onClick={() => changeQuantity(line.product.id, 1)}
                        className="rounded border border-slate-300 bg-white p-1 hover:bg-slate-100"
                      >
                        <Plus className="h-3 w-3" />
                      </button>
                      <button
                        type="button"
                        onClick={() => setCart((c) => c.filter((l) => l.product.id !== line.product.id))}
                        className="ml-1 rounded p-1 text-slate-400 hover:text-rose-600"
                      >
                        <Trash2 className="h-3.5 w-3.5" />
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </section>

        {/* Thông tin giao hàng */}
        <section>
          <h3 className="mb-2 text-sm font-semibold text-slate-800">2. Thông tin giao hàng</h3>

          <form className="space-y-3">
            <Input label="Người nhận" {...form.register('recipientName', { required: true })} />
            <Input label="Số điện thoại" placeholder="0901234567" {...form.register('phone', { required: true })} />
            <Input label="Địa chỉ" placeholder="Số nhà, tên đường" {...form.register('street', { required: true })} />

            <div className="grid grid-cols-2 gap-3">
              <Input label="Phường/Xã" {...form.register('ward')} />
              <Input label="Quận/Huyện" {...form.register('district')} />
            </div>

            <Input label="Tỉnh/Thành phố" {...form.register('city', { required: true })} />
            <Input label="Ghi chú" {...form.register('note')} />

            <Select label="Phương thức thanh toán" {...form.register('paymentMethod')}>
              {paymentMethods.map((method) => (
                <option key={method.value} value={method.value}>{method.label}</option>
              ))}
            </Select>
          </form>

          <div className="mt-4 rounded-lg bg-brand-50 p-3">
            <div className="flex justify-between text-sm">
              <span className="text-slate-600">Tổng cộng</span>
              <span className="font-semibold text-slate-900">{formatMoney(total, currency)}</span>
            </div>
            <p className="mt-1.5 text-xs text-slate-500">
              Mẹo thử nghiệm: tổng tiền chia hết cho 13 sẽ bị cổng thanh toán từ chối —
              dùng để xem luồng bồi hoàn tự động nhả kho.
            </p>
          </div>
        </section>
      </div>
    </Modal>
  )
}
