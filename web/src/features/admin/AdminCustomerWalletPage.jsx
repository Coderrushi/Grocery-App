import { useForm, useWatch } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Link, useParams } from 'react-router'
import { Button } from '../../shared/ui/Button'
import { Alert, ErrorAlert } from '../../shared/ui/Alert'
import { Card } from '../../shared/ui/Card'
import { Spinner } from '../../shared/ui/Spinner'
import { SelectField, TextField } from '../../shared/ui/forms'
import { applyServerErrors, formLevelError } from '../../shared/api/formErrors'
import { formatCurrency } from '../../shared/utils/format'
import { TransactionList } from '../wallet/TransactionList'
import { useAdjustWallet, useAdminWallet } from '../wallet/walletApi'

const schema = z.object({
  direction: z.enum(['Credit', 'Debit']),
  amount: z
    .number({ error: 'Enter an amount.' })
    .positive('Enter an amount above ₹0.')
    .max(100000)
    .refine((v) => /^\d+(\.\d{1,2})?$/.test(String(v)), 'Use at most 2 decimal places (paise).'),
  reason: z.enum(['Refund', 'Goodwill', 'Correction']),
  note: z.string().trim().min(5, 'Explain why, e.g. the order number or complaint.').max(200),
})

function AdjustmentForm({ userId, balance }) {
  const adjust = useAdjustWallet(userId)
  const {
    register,
    handleSubmit,
    reset,
    setError,
    control,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(schema),
    defaultValues: { direction: 'Credit', amount: undefined, reason: 'Refund', note: '' },
  })
  const direction = useWatch({ control, name: 'direction' })

  const onSubmit = (values) => {
    const verb = values.direction === 'Credit' ? 'Add' : 'Remove'
    if (
      !window.confirm(
        `${verb} ${formatCurrency(values.amount)} ${values.direction === 'Credit' ? 'to' : 'from'} this wallet?`,
      )
    )
      return
    adjust.mutate(values, { onSuccess: () => reset(), onError: (e) => applyServerErrors(e, setError) })
  }

  return (
    <Card title="Adjust balance" description="Every adjustment is recorded with your name and the note.">
      <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-4">
        <ErrorAlert error={formLevelError(adjust.error)} title="Couldn't adjust the wallet" />
        {adjust.isSuccess && <Alert tone="success">Done. New balance {formatCurrency(adjust.data.balance)}.</Alert>}
        <fieldset className="flex gap-2">
          <legend className="sr-only">Direction</legend>
          {['Credit', 'Debit'].map((d) => (
            <label
              key={d}
              className="border-line has-checked:border-brand-600 has-checked:bg-brand-50 flex h-10 cursor-pointer items-center rounded-full border px-4 text-sm font-semibold"
            >
              <input type="radio" value={d} className="sr-only" {...register('direction')} />
              {d === 'Credit' ? '➕ Add money' : '➖ Remove money'}
            </label>
          ))}
        </fieldset>
        <div className="grid gap-4 sm:grid-cols-2">
          <TextField
            label="Amount (₹)"
            type="number"
            step="0.01"
            inputMode="decimal"
            hint={direction === 'Debit' ? `Available: ${formatCurrency(balance)}` : undefined}
            error={errors.amount}
            {...register('amount', { valueAsNumber: true })}
          />
          <SelectField label="Reason" error={errors.reason} {...register('reason')}>
            <option value="Refund">Refund</option>
            <option value="Goodwill">Goodwill gesture</option>
            <option value="Correction">Correction</option>
          </SelectField>
        </div>
        <TextField
          label="Note"
          placeholder="e.g. Order #1042, eggs arrived broken"
          error={errors.note}
          {...register('note')}
        />
        <Button type="submit" variant={direction === 'Debit' ? 'danger' : 'primary'} disabled={adjust.isPending}>
          {adjust.isPending ? 'Saving…' : direction === 'Credit' ? 'Add money' : 'Remove money'}
        </Button>
      </form>
    </Card>
  )
}

export function AdminCustomerWalletPage() {
  const userId = Number(useParams().id)
  const { data: wallet, isPending, error } = useAdminWallet(userId)

  if (isPending) return <Spinner />
  if (error) return <ErrorAlert error={error} title="Couldn't load this customer" />

  return (
    <div className="space-y-6">
      <Link to="/admin/customers" className="text-brand-700 text-sm font-semibold">
        ← All customers
      </Link>
      <div>
        <h1 className="text-2xl font-bold">{wallet.fullName}</h1>
        <p className="text-ink-muted text-sm">{[wallet.phoneNumber, wallet.email].filter(Boolean).join(' · ')}</p>
      </div>
      <div className="grid gap-6 lg:grid-cols-[1fr_22rem]">
        <Card title="Wallet transactions">
          <p className="mb-2 text-3xl font-bold tabular-nums">{formatCurrency(wallet.balance)}</p>
          <TransactionList userId={userId} />
        </Card>
        <AdjustmentForm userId={userId} balance={wallet.balance} />
      </div>
    </div>
  )
}
