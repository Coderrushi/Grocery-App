import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { Button } from '../../shared/ui/Button'
import { Alert, ErrorAlert } from '../../shared/ui/Alert'
import { Card } from '../../shared/ui/Card'
import { Spinner } from '../../shared/ui/Spinner'
import { CheckboxField, SelectField, TextAreaField, TextField } from '../../shared/ui/forms'
import { applyServerErrors, formLevelError } from '../../shared/api/formErrors'
import { useAdminCategories, useAdminProduct, useSaveProduct } from './adminCatalogApi'
import { ProductImagePanel } from './ProductImagePanel'
import { StockPanel } from './StockPanel'

const rupees = (label) =>
  z
    .number({ error: `Enter the ${label}.` })
    .positive(`Enter a ${label} above ₹0.`)
    .max(100000)
    .refine((v) => /^\d+(\.\d{1,2})?$/.test(String(v)), 'Use at most 2 decimal places (paise).')

const optional = (max) => z.string().trim().max(max)

const schema = z
  .object({
    categoryId: z.number({ error: 'Select a category.' }).int().positive('Select a category.'),
    name: z.string().trim().min(1, 'Enter the product name.').max(150),
    brand: optional(60),
    unitLabel: z.string().trim().min(1, 'Enter the unit, e.g. "1 kg" or "500 ml".').max(30),
    sku: optional(40),
    price: rupees('price'),
    mrp: rupees('MRP'),
    lowStockThreshold: z.number({ error: 'Enter a number.' }).int().min(0).max(1000),
    maxPerOrder: z.number({ error: 'Enter a number.' }).int().min(1).max(100),
    initialStock: z.number({ error: 'Enter a number.' }).int().min(0).max(100000).optional(),
    description: optional(2000),
    isActive: z.boolean(),
    isFeatured: z.boolean(),
  })
  .refine((v) => v.mrp >= v.price, { path: ['mrp'], message: "MRP can't be lower than the selling price." })

const newProductDefaults = {
  categoryId: undefined,
  name: '',
  brand: '',
  unitLabel: '',
  sku: '',
  price: undefined,
  mrp: undefined,
  lowStockThreshold: 5,
  maxPerOrder: 10,
  initialStock: 0,
  description: '',
  isActive: true,
  isFeatured: false,
}

const toFormValues = (p) => ({
  ...p,
  brand: p.brand ?? '',
  sku: p.sku ?? '',
  description: p.description ?? '',
})

function ProductDetailsForm({ product, justSaved, onSaved, onReload }) {
  const isNew = !product
  const { data: categories } = useAdminCategories()
  const save = useSaveProduct()
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isDirty },
  } = useForm({
    resolver: zodResolver(schema),
    defaultValues: isNew ? newProductDefaults : toFormValues(product),
  })

  const onSubmit = ({ initialStock, ...values }) =>
    save.mutate(isNew ? { ...values, initialStock } : { id: product.id, ...values, version: product.version }, {
      onSuccess: onSaved,
      onError: (e) => applyServerErrors(e, setError),
    })

  const conflict = save.error?.status === 409

  return (
    <Card title={isNew ? 'New product' : 'Product details'}>
      <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-4">
        {conflict ? (
          <Alert tone="error" title="Someone else changed this product">
            <p>Your changes were not saved, so nothing was overwritten. Reload to see the latest version.</p>
            <Button size="sm" variant="secondary" className="mt-2" onClick={onReload}>
              Reload product
            </Button>
          </Alert>
        ) : (
          <ErrorAlert error={formLevelError(save.error)} title="Couldn't save the product" />
        )}
        {justSaved && !isDirty && !save.error && <Alert tone="success">Saved.</Alert>}

        <div className="grid gap-4 sm:grid-cols-2">
          <TextField label="Product name" error={errors.name} className="sm:col-span-2" {...register('name')} />
          <SelectField
            label="Category"
            error={errors.categoryId}
            {...register('categoryId', { setValueAs: (v) => (v === '' ? undefined : Number(v)) })}
          >
            <option value="">Select…</option>
            {categories?.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
                {c.isActive ? '' : ' (hidden)'}
              </option>
            ))}
          </SelectField>
          <TextField label="Brand (optional)" error={errors.brand} {...register('brand')} />
          <TextField
            label="Unit"
            placeholder="1 kg, 500 ml, Pack of 6"
            error={errors.unitLabel}
            {...register('unitLabel')}
          />
          <TextField label="SKU / barcode (optional)" error={errors.sku} {...register('sku')} />
          <TextField
            label="Selling price (₹)"
            type="number"
            step="0.01"
            inputMode="decimal"
            error={errors.price}
            {...register('price', { valueAsNumber: true })}
          />
          <TextField
            label="MRP (₹)"
            type="number"
            step="0.01"
            inputMode="decimal"
            hint="As printed on the pack."
            error={errors.mrp}
            {...register('mrp', { valueAsNumber: true })}
          />
          <TextField
            label="Low-stock warning at"
            type="number"
            error={errors.lowStockThreshold}
            {...register('lowStockThreshold', { valueAsNumber: true })}
          />
          <TextField
            label="Max per order"
            type="number"
            error={errors.maxPerOrder}
            {...register('maxPerOrder', { valueAsNumber: true })}
          />
          {isNew && (
            <TextField
              label="Opening stock"
              type="number"
              hint="Later changes go through stock adjustments."
              error={errors.initialStock}
              {...register('initialStock', { valueAsNumber: true })}
            />
          )}
        </div>
        <TextAreaField label="Description (optional)" error={errors.description} {...register('description')} />
        <div className="flex flex-wrap gap-6">
          <CheckboxField label="Visible to customers" {...register('isActive')} />
          <CheckboxField label="Feature on the home page" {...register('isFeatured')} />
        </div>
        <Button type="submit" disabled={save.isPending || (!isNew && !isDirty)}>
          {save.isPending ? 'Saving…' : isNew ? 'Create product' : 'Save changes'}
        </Button>
      </form>
    </Card>
  )
}

export function AdminProductFormPage() {
  const { id } = useParams()
  const productId = id ? Number(id) : null
  const navigate = useNavigate()
  const { data: product, isPending, error, refetch } = useAdminProduct(productId)
  const [savedVersion, setSavedVersion] = useState(null)

  if (productId && isPending) return <Spinner />
  if (productId && error) return <ErrorAlert error={error} title="Couldn't load this product" />

  return (
    <div className="space-y-6">
      <Link to="/admin/products" className="text-brand-700 text-sm font-semibold">
        ← All products
      </Link>
      {product && <h1 className="text-2xl font-bold">{product.name}</h1>}

      <div className="grid gap-6 lg:grid-cols-[1fr_20rem]">
        <ProductDetailsForm
          // Reset the form only when the product's version changes (our save, or a reload after someone
          // else's). Stock and photo changes don't touch the version, so they never wipe unsaved edits.
          key={productId ? `${productId}-v${product.version}` : 'new'}
          justSaved={product != null && savedVersion === product.version}
          product={product}
          onSaved={(saved) => {
            setSavedVersion(saved.version)
            if (!productId) navigate(`/admin/products/${saved.id}`, { replace: true })
          }}
          onReload={() => refetch()}
        />
        {product && (
          <div className="space-y-6">
            <ProductImagePanel product={product} />
            <StockPanel product={product} />
          </div>
        )}
      </div>
    </div>
  )
}
