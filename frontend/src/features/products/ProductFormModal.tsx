import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Modal } from '@/components/ui/Modal'
import { Button } from '@/components/ui/Button'
import { Input, Select, Textarea } from '@/components/ui/Field'
import { useToast } from '@/components/ui/Toast'
import { categoryApi, productApi } from './product-api'
import { ApiException } from '@/lib/api-client'

const schema = z.object({
  sku: z.string().min(1, 'Nhập SKU').max(50).regex(/^[A-Za-z0-9\-_]+$/, "Chỉ gồm chữ, số, '-' và '_'"),
  name: z.string().min(1, 'Nhập tên sản phẩm').max(200),
  description: z.string().max(2000).optional(),
  price: z.coerce.number().positive('Giá phải lớn hơn 0'),
  categoryId: z.string().min(1, 'Chọn danh mục'),
  imageUrl: z.string().url('URL không hợp lệ').optional().or(z.literal('')),
  initialStock: z.coerce.number().int().min(0, 'Không được âm'),
})

type FormValues = z.infer<typeof schema>

interface Props {
  open: boolean
  productId: string | null   // null = tạo mới
  onClose: () => void
}

export function ProductFormModal({ open, productId, onClose }: Props) {
  const isEdit = productId !== null
  const queryClient = useQueryClient()
  const toast = useToast()

  const categoriesQuery = useQuery({
    queryKey: ['categories', { onlyActive: true }],
    queryFn: () => categoryApi.list(true),
    enabled: open,
  })

  const productQuery = useQuery({
    queryKey: ['products', productId],
    queryFn: () => productApi.getById(productId!),
    enabled: open && isEdit,
  })

  const form = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { sku: '', name: '', description: '', price: 0, categoryId: '', imageUrl: '', initialStock: 0 },
  })

  // Nạp dữ liệu khi mở form sửa; reset sạch khi mở form tạo mới.
  useEffect(() => {
    if (!open) return

    if (isEdit && productQuery.data) {
      const p = productQuery.data
      form.reset({
        sku: p.sku,
        name: p.name,
        description: p.description ?? '',
        price: p.price,
        categoryId: p.categoryId,
        imageUrl: p.imageUrl ?? '',
        initialStock: 0,
      })
    } else if (!isEdit) {
      form.reset({ sku: '', name: '', description: '', price: 0, categoryId: '', imageUrl: '', initialStock: 0 })
    }
  }, [open, isEdit, productQuery.data, form])

  const mutation = useMutation({
    mutationFn: async (values: FormValues) => {
      if (isEdit) {
        const updated = await productApi.update(productId, {
          name: values.name,
          description: values.description || undefined,
          categoryId: values.categoryId,
          imageUrl: values.imageUrl || undefined,
        })

        // Giá đi qua endpoint riêng vì nó phát integration event cho các service khác.
        if (productQuery.data && productQuery.data.price !== values.price) {
          await productApi.changePrice(productId, values.price)
        }
        return updated
      }

      return productApi.create({
        sku: values.sku,
        name: values.name,
        description: values.description || undefined,
        price: values.price,
        currency: 'VND',
        categoryId: values.categoryId,
        imageUrl: values.imageUrl || undefined,
        initialStock: values.initialStock,
      })
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['products'] })
      queryClient.invalidateQueries({ queryKey: ['stocks'] })
      toast.success(isEdit ? 'Đã cập nhật sản phẩm' : 'Đã tạo sản phẩm mới')
      onClose()
    },
    onError: (error) => {
      toast.error('Lưu thất bại', error instanceof ApiException ? error.message : undefined)
    },
  })

  return (
    <Modal
      open={open}
      title={isEdit ? 'Sửa sản phẩm' : 'Thêm sản phẩm'}
      description={
        isEdit
          ? 'Đổi giá sẽ phát sự kiện tới các service khác.'
          : 'Tồn kho ban đầu được Inventory service tạo tự động qua Kafka.'
      }
      onClose={onClose}
      footer={
        <>
          <Button variant="outline" onClick={onClose}>Hủy</Button>
          <Button onClick={form.handleSubmit((v) => mutation.mutate(v))} loading={mutation.isPending}>
            {isEdit ? 'Lưu thay đổi' : 'Tạo sản phẩm'}
          </Button>
        </>
      }
    >
      <form className="space-y-4" onSubmit={form.handleSubmit((v) => mutation.mutate(v))}>
        <div className="grid gap-4 sm:grid-cols-2">
          <Input
            label="SKU"
            disabled={isEdit}
            hint={isEdit ? 'SKU không thể thay đổi sau khi tạo' : 'Ví dụ: LAP-001'}
            error={form.formState.errors.sku?.message}
            {...form.register('sku')}
          />

          <Select label="Danh mục" error={form.formState.errors.categoryId?.message} {...form.register('categoryId')}>
            <option value="">— Chọn danh mục —</option>
            {categoriesQuery.data?.map((category) => (
              <option key={category.id} value={category.id}>{category.name}</option>
            ))}
          </Select>
        </div>

        <Input label="Tên sản phẩm" error={form.formState.errors.name?.message} {...form.register('name')} />

        <Textarea label="Mô tả" rows={3} error={form.formState.errors.description?.message} {...form.register('description')} />

        <div className="grid gap-4 sm:grid-cols-2">
          <Input
            label="Giá (VND)"
            type="number"
            step="1000"
            error={form.formState.errors.price?.message}
            {...form.register('price')}
          />

          {!isEdit && (
            <Input
              label="Tồn kho ban đầu"
              type="number"
              hint="Inventory service sẽ tạo bản ghi kho"
              error={form.formState.errors.initialStock?.message}
              {...form.register('initialStock')}
            />
          )}
        </div>

        <Input
          label="Ảnh sản phẩm (URL)"
          placeholder="https://..."
          error={form.formState.errors.imageUrl?.message}
          {...form.register('imageUrl')}
        />
      </form>
    </Modal>
  )
}
