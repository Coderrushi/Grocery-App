import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Button } from '../../shared/ui/Button'
import { ErrorAlert } from '../../shared/ui/Alert'
import { Card } from '../../shared/ui/Card'
import { SelectField, TextField } from '../../shared/ui/forms'
import { applyServerErrors, formLevelError } from '../../shared/api/formErrors'
import { formatDateTime } from '../../shared/utils/format'
import { useAdjustStock, useStockHistory } from './adminCatalogApi'

// Reasons the shopkeeper can pick, and whether they add (+1) or remove (-1) stock.
const REASONS = [
  { value: 'Restock', label: 'Restock (new delivery)', sign: 1 },
  { value: 'Returned', label: 'Customer return', sign: 1 },
  { value: 'Damaged', label: 'Damaged', sign: -1 },
  { value: 'Expired', label: 'Expired', sign: -1 },
  { value: 'Correction', label: 'Stock-count correction (+/−)', sign: 0 },
]

const REASON_LABELS = {
  InitialStock: 'Opening stock',
  Sale: 'Sold (order)',
  OrderCancelled: 'Order cancelled (returned)',
  ...Object.fromEntries(REASONS.map((r) => [r.value, r.label])),
}

const schema = z.object({
  reason: z.enum(REASONS.map((r) => r.value)),
  quantity: z
    .number({ error: 'Enter a quantity.' })
    .int('Whole units only.')
    .refine((v) => v !== 0, 'Enter a quantity.'),
  note: z.string().trim().max(200),
})

/** Restock and removal reasons take a positive number and apply the sign; corrections take +/− as typed. */
function signedChange({ reason, quantity }) {
  const sign = REASONS.find((r) => r.value === reason).sign
  return sign === 0 ? quantity : sign * Math.abs(quantity)
}

function StockHistory({ productId }) {
  const { data: history } = useStockHistory(productId)
  if (!history?.length) return null
  return (
    <details className="mt-4">
      <summary className="cursor-pointer text-sm font-semibold">Stock history</summary>
      <ul className="divide-line mt-2 divide-y text-sm">
        {history.map((h) => (
          <li key={h.id} className="py-2">
            <div className="flex justify-between gap-2">
              <span>{REASON_LABELS[h.reason] ?? h.reason}</span>
              <span className={`font-semibold ${h.quantityChange > 0 ? 'text-success-600' : 'text-danger-700'}`}>
                {h.quantityChange > 0 ? '+' : ''}
                {h.quantityChange} → {h.quantityAfter}
              </span>
            </div>
            <p className="text-ink-muted text-xs">
              {formatDateTime(h.createdAtUtc)}
              {h.createdBy && ` · ${h.createdBy}`}
              {h.note && ` · ${h.note}`}
            </p>
          </li>
        ))}
      </ul>
    </details>
  )
}

export function StockPanel({ product }) {
  const adjust = useAdjustStock()
  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors },
  } = useForm({ resolver: zodResolver(schema), defaultValues: { reason: 'Restock', quantity: undefined, note: '' } })

  const onSubmit = (values) =>
    adjust.mutate(
      { id: product.id, quantityChange: signedChange(values), reason: values.reason, note: values.note },
      {
        onSuccess: () => reset(),
        onError: (e) => {
          // The API reports the quantity field as "quantityChange"; show it under our "quantity" input.
          if (e?.errors?.quantityChange) setError('quantity', { message: e.errors.quantityChange[0] })
          else applyServerErrors(e, setError)
        },
      },
    )

  const tone =
    product.stockStatus === 'OutOfStock'
      ? 'text-danger-700'
      : product.stockStatus === 'LowStock'
        ? 'text-accent-600'
        : 'text-ink'

  return (
    <Card title="Stock">
      <p className={`text-3xl font-bold ${tone}`}>
        {product.stockQuantity} <span className="text-ink-muted text-base font-normal">in stock</span>
      </p>
      <form onSubmit={handleSubmit(onSubmit)} noValidate className="mt-4 space-y-3">
        <ErrorAlert error={formLevelError(adjust.error)} title="Couldn't adjust stock" />
        <SelectField label="Reason" error={errors.reason} {...register('reason')}>
          {REASONS.map((r) => (
            <option key={r.value} value={r.value}>
              {r.label}
            </option>
          ))}
        </SelectField>
        <TextField
          label="Quantity"
          type="number"
          inputMode="numeric"
          hint="For corrections, use a minus sign to remove units."
          error={errors.quantity}
          {...register('quantity', { valueAsNumber: true })}
        />
        <TextField label="Note (optional)" error={errors.note} {...register('note')} />
        <Button type="submit" size="sm" disabled={adjust.isPending}>
          {adjust.isPending ? 'Updating…' : 'Update stock'}
        </Button>
      </form>
      <StockHistory productId={product.id} />
    </Card>
  )
}
