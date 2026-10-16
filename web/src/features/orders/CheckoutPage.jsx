import { useEffect, useRef, useState } from 'react'
import { Link, Navigate, useNavigate } from 'react-router'
import { Button } from '../../shared/ui/Button'
import { Alert, ErrorAlert } from '../../shared/ui/Alert'
import { Card } from '../../shared/ui/Card'
import { Spinner } from '../../shared/ui/Spinner'
import { toast } from '../../shared/ui/toast'
import { formatCurrency } from '../../shared/utils/format'
import { config } from '../../app/config'
import { formatAddress } from '../account/formatAddress'
import { BillLines } from '../cart/BillLines'
import { newIdempotencyKey } from '../wallet/walletApi'
import { useCheckoutSummary, usePlaceOrder } from './ordersApi'

function AddressPicker({ addresses, selectedId, onSelect }) {
  if (addresses.length === 0) {
    return (
      <div className="text-center">
        <p className="text-ink-muted text-sm">Add the address we should deliver to.</p>
        <Link
          to="/account/addresses/new?returnTo=%2Fcheckout"
          className="bg-brand-600 hover:bg-brand-700 mt-3 inline-flex h-11 items-center rounded-lg px-4 font-semibold text-white"
        >
          + Add delivery address
        </Link>
      </div>
    )
  }

  return (
    <fieldset className="space-y-2">
      <legend className="sr-only">Delivery address</legend>
      {addresses.map(({ address, isServiceable }) => (
        <label
          key={address.id}
          className={`border-line has-checked:border-brand-600 has-checked:bg-brand-50 flex gap-3 rounded-lg border p-3 ${
            isServiceable ? 'cursor-pointer' : 'cursor-not-allowed opacity-60'
          }`}
        >
          <input
            type="radio"
            name="address"
            className="accent-brand-600 mt-1 size-4"
            checked={selectedId === address.id}
            disabled={!isServiceable}
            onChange={() => onSelect(address.id)}
          />
          <span className="text-sm">
            <span className="font-semibold">{address.label}</span>
            {address.isDefault && <span className="text-ink-muted"> · Default</span>}
            <span className="block">
              {address.recipientName} · {address.phoneNumber}
            </span>
            <span className="text-ink-muted block">{formatAddress(address)}</span>
            {!isServiceable && (
              <span className="text-danger-700 block font-semibold">We don't deliver to this PIN code yet.</span>
            )}
          </span>
        </label>
      ))}
      <Link
        to="/account/addresses/new?returnTo=%2Fcheckout"
        className="text-brand-700 inline-block text-sm font-semibold"
      >
        + Add another address
      </Link>
    </fieldset>
  )
}

export function CheckoutPage() {
  const { data: summary, isPending, error, isFetching } = useCheckoutSummary()
  const place = usePlaceOrder()
  const navigate = useNavigate()
  const [addressId, setAddressId] = useState(null)
  const [instructions, setInstructions] = useState('')
  // One key per attempt: kept if the connection drops (so a retry can't create a second order).
  const attemptKey = useRef(newIdempotencyKey())

  useEffect(() => {
    document.title = `Checkout | ${config.storeName}`
  }, [])

  if (isPending) return <Spinner label="Preparing checkout…" />
  if (error) return <ErrorAlert error={error} title="Couldn't load checkout" />
  if (summary.cart.lines.length === 0 && !place.isSuccess) return <Navigate to="/cart" replace />

  const { cart, addresses, walletBalance, hasEnoughBalance } = summary
  const serviceable = addresses.filter((a) => a.isServiceable).map((a) => a.address)
  const preferred = serviceable.find((a) => a.isDefault) ?? serviceable[0]
  const selectedId = addressId ?? preferred?.id ?? null
  const shortfall = Math.max(0, cart.totals.total - walletBalance)
  const canPlace = cart.canCheckout && selectedId != null && hasEnoughBalance && !isFetching

  const placeOrder = () =>
    place.mutate(
      {
        addressId: selectedId,
        deliveryInstructions: instructions.trim() || null,
        expectedTotal: cart.totals.total,
        checkoutKey: attemptKey.current,
      },
      {
        onSuccess: (order) => navigate(`/account/orders/${order.id}?placed=1`, { replace: true }),
        onError: (e) => {
          if (!e.isNetworkError) attemptKey.current = newIdempotencyKey()
          if (e.status === 409) toast(e.detail, { tone: 'error', durationMs: 7000 })
        },
      },
    )

  return (
    <div className="grid gap-6 lg:grid-cols-[1fr_22rem]">
      <div className="space-y-6">
        <h1 className="text-2xl font-bold">Checkout</h1>

        <Card title="1. Delivery address">
          <AddressPicker addresses={addresses} selectedId={selectedId} onSelect={setAddressId} />
        </Card>

        <Card
          title="2. Delivery instructions"
          description="Optional. For example: “Ring the bell twice” or “Leave with security”."
        >
          <label htmlFor="instructions" className="sr-only">
            Delivery instructions
          </label>
          <textarea
            id="instructions"
            rows={2}
            maxLength={200}
            value={instructions}
            onChange={(e) => setInstructions(e.target.value)}
            className="border-line bg-surface focus:border-brand-600 focus:ring-brand-100 block w-full rounded-lg border px-3 py-2 text-base focus:ring-2 focus:outline-none"
          />
        </Card>

        <Card title="3. Items">
          <ul className="divide-line divide-y text-sm">
            {cart.lines.map((line) => (
              <li key={line.productId} className="flex justify-between gap-3 py-2">
                <span>
                  {line.name} <span className="text-ink-muted">({line.unitLabel})</span> × {line.quantity}
                </span>
                <span className="font-medium">{formatCurrency(line.lineTotal)}</span>
              </li>
            ))}
          </ul>
          <Link to="/cart" className="text-brand-700 mt-2 inline-block text-sm font-semibold">
            Edit cart
          </Link>
        </Card>
      </div>

      <div className="space-y-4 lg:sticky lg:top-24 lg:self-start">
        <Card title="Payment">
          <BillLines totals={cart.totals} />
          <div className="bg-canvas mt-4 flex items-center justify-between rounded-lg px-3 py-2 text-sm">
            <span>👛 Wallet balance</span>
            <span className="font-semibold">{formatCurrency(walletBalance)}</span>
          </div>
          {!hasEnoughBalance && (
            <Alert tone="error" title="Not enough wallet balance">
              Add at least {formatCurrency(shortfall)} to pay for this order.{' '}
              <Link to="/account/wallet" className="font-semibold underline">
                Add money
              </Link>
            </Alert>
          )}
          {cart.hasIssues && (
            <Alert tone="error">
              Some items changed.{' '}
              <Link to="/cart" className="font-semibold underline">
                Review your cart
              </Link>
            </Alert>
          )}
          {!cart.totals.meetsMinimumOrder && (
            <Alert tone="error">The minimum order is {formatCurrency(cart.totals.minimumOrderAmount)}.</Alert>
          )}
          <div className="mt-4 space-y-2">
            {place.error && place.error.status !== 409 && (
              <ErrorAlert error={place.error} title="Couldn't place your order" />
            )}
            <Button className="w-full" size="lg" disabled={!canPlace || place.isPending} onClick={placeOrder}>
              {place.isPending ? 'Placing order…' : `Pay ${formatCurrency(cart.totals.total)} & place order`}
            </Button>
            <p className="text-ink-muted text-center text-xs">
              Paid from your wallet. You can cancel until the store confirms.
            </p>
          </div>
        </Card>
      </div>
    </div>
  )
}
