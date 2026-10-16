import { useRef, useState } from 'react'
import { Button } from '../../shared/ui/Button'
import { Alert, ErrorAlert } from '../../shared/ui/Alert'
import { Card } from '../../shared/ui/Card'
import { Spinner } from '../../shared/ui/Spinner'
import { formatCurrency } from '../../shared/utils/format'
import { TransactionList } from './TransactionList'
import { newIdempotencyKey, useTopUp, useWallet } from './walletApi'

const QUICK_AMOUNTS = [100, 200, 500, 1000]

function TopUpForm({ wallet }) {
  const [amount, setAmount] = useState('')
  const [validation, setValidation] = useState(null)
  const topUp = useTopUp()
  // One key per attempt. Kept when the network drops (so a retry can't double-charge), replaced once
  // the server has answered or the amount changes.
  const attemptKey = useRef(newIdempotencyKey())

  const room = Math.max(0, wallet.maxBalance - wallet.balance)
  const value = Number(amount)

  const chooseAmount = (next) => {
    setAmount(String(next))
    setValidation(null)
    topUp.reset()
    attemptKey.current = newIdempotencyKey()
  }

  const submit = (event) => {
    event.preventDefault()
    if (!/^\d+(\.\d{1,2})?$/.test(amount) || value < wallet.minTopUp || value > wallet.maxTopUp) {
      setValidation(
        `Enter an amount between ${formatCurrency(wallet.minTopUp)} and ${formatCurrency(wallet.maxTopUp)}.`,
      )
      return
    }
    if (value > room) {
      setValidation(
        `Your wallet can hold up to ${formatCurrency(wallet.maxBalance)}. You can add ${formatCurrency(room)}.`,
      )
      return
    }
    setValidation(null)
    topUp.mutate(
      { amount: value, idempotencyKey: attemptKey.current },
      {
        onSettled: (_data, error) => {
          if (!error?.isNetworkError) attemptKey.current = newIdempotencyKey()
        },
        onSuccess: (result) => result.status === 'Succeeded' && setAmount(''),
      },
    )
  }

  const result = topUp.data
  return (
    <Card title="Add money" description="Use your wallet balance to pay for orders in one tap.">
      <form onSubmit={submit} noValidate className="space-y-4">
        {result?.status === 'Succeeded' && (
          <Alert tone="success">
            {formatCurrency(result.amount)} added. New balance {formatCurrency(result.balance)}.
          </Alert>
        )}
        {result?.status === 'Failed' && (
          <Alert tone="error" title="Payment failed">
            {result.failureReason} No money was added and you have not been charged.
          </Alert>
        )}
        <ErrorAlert error={topUp.error} title="Couldn't add money" />

        <div className="flex flex-wrap gap-2" role="group" aria-label="Quick amounts">
          {QUICK_AMOUNTS.map((quick) => (
            <button
              key={quick}
              type="button"
              disabled={quick > room}
              onClick={() => chooseAmount(quick)}
              aria-pressed={value === quick}
              className="border-line aria-pressed:border-brand-600 aria-pressed:bg-brand-50 aria-pressed:text-brand-900 h-10 rounded-full border px-4 text-sm font-semibold disabled:opacity-40"
            >
              {formatCurrency(quick).replace('.00', '')}
            </button>
          ))}
        </div>

        <div>
          <label htmlFor="top-up-amount" className="mb-1 block text-sm font-medium">
            Amount (₹)
          </label>
          <input
            id="top-up-amount"
            type="number"
            inputMode="decimal"
            min={wallet.minTopUp}
            max={wallet.maxTopUp}
            step="0.01"
            value={amount}
            onChange={(e) => chooseAmount(e.target.value)}
            aria-invalid={validation ? 'true' : undefined}
            aria-describedby="top-up-help"
            className={`bg-surface focus:border-brand-600 focus:ring-brand-100 h-11 w-full max-w-xs rounded-lg border px-3 text-base focus:ring-2 focus:outline-none ${validation ? 'border-danger-600' : 'border-line'}`}
          />
          <p id="top-up-help" className={`mt-1 text-sm ${validation ? 'text-danger-700' : 'text-ink-muted'}`}>
            {validation ?? `${formatCurrency(wallet.minTopUp)} – ${formatCurrency(wallet.maxTopUp)} per top-up.`}
          </p>
        </div>

        <Button type="submit" disabled={topUp.isPending || !amount}>
          {topUp.isPending ? 'Processing…' : amount ? `Add ${formatCurrency(value || 0)}` : 'Add money'}
        </Button>
        <p className="text-ink-muted text-xs">🧪 Payments are simulated in this version. No real money is charged.</p>
      </form>
    </Card>
  )
}

export function WalletPage() {
  const { data: wallet, isPending, error } = useWallet()

  if (isPending) return <Spinner label="Loading your wallet…" />
  if (error) return <ErrorAlert error={error} title="Couldn't load your wallet" />

  return (
    <div className="space-y-6">
      <section className="rounded-card bg-brand-600 shadow-card p-6 text-white">
        <p className="text-sm font-semibold text-white/80">Wallet balance</p>
        <p className="mt-1 text-4xl font-bold tabular-nums">{formatCurrency(wallet.balance)}</p>
        <p className="mt-2 text-xs text-white/80">Usable only at this store. Wallet balance can't be withdrawn.</p>
      </section>
      <TopUpForm wallet={wallet} />
      <Card title="Transactions">
        <TransactionList />
      </Card>
    </div>
  )
}
