import { Link } from 'react-router'
import { Button } from '../../shared/ui/Button'
import { ErrorAlert } from '../../shared/ui/Alert'
import { Spinner } from '../../shared/ui/Spinner'
import { formatAddress } from './formatAddress'
import { useAddresses, useDeleteAddress, useSetDefaultAddress } from './accountApi'

const MAX_ADDRESSES = 10
const labelIcons = { Home: '🏠', Work: '🏢', Other: '📍' }

function AddressCard({ address }) {
  const remove = useDeleteAddress()
  const makeDefault = useSetDefaultAddress()
  const busy = remove.isPending || makeDefault.isPending

  const onDelete = () => {
    if (window.confirm('Delete this address?')) remove.mutate(address.id)
  }

  return (
    <li className="rounded-card border-line bg-surface shadow-card border p-4">
      <div className="flex flex-wrap items-center gap-2">
        <span className="font-semibold">
          <span aria-hidden="true">{labelIcons[address.label]} </span>
          {address.label}
        </span>
        {address.isDefault && (
          <span className="bg-brand-100 text-brand-900 rounded-full px-2 py-0.5 text-xs font-semibold">Default</span>
        )}
      </div>
      <p className="mt-2 text-sm">
        {address.recipientName} · {address.phoneNumber}
      </p>
      <p className="text-ink-muted mt-1 text-sm">{formatAddress(address)}</p>
      <ErrorAlert error={remove.error ?? makeDefault.error} />
      <div className="mt-3 flex flex-wrap gap-2">
        <Link
          to={`/account/addresses/${address.id}/edit`}
          className="border-line hover:bg-canvas inline-flex h-9 items-center rounded-lg border px-3 text-sm font-semibold"
        >
          Edit
        </Link>
        {!address.isDefault && (
          <Button size="sm" variant="ghost" disabled={busy} onClick={() => makeDefault.mutate(address.id)}>
            Set as default
          </Button>
        )}
        <Button size="sm" variant="ghost" className="text-danger-700" disabled={busy} onClick={onDelete}>
          Delete
        </Button>
      </div>
    </li>
  )
}

export function AddressesPage() {
  const { data: addresses, isPending, error } = useAddresses()

  if (isPending) return <Spinner label="Loading addresses…" />
  if (error) return <ErrorAlert error={error} title="Couldn't load your addresses" />

  const canAdd = addresses.length < MAX_ADDRESSES

  return (
    <div>
      <div className="flex items-center justify-between gap-4">
        <p className="text-ink-muted text-sm">
          {addresses.length === 0
            ? 'Add an address to get your groceries delivered.'
            : 'Your saved delivery addresses.'}
        </p>
        {canAdd && (
          <Link
            to="/account/addresses/new"
            className="bg-brand-600 hover:bg-brand-700 inline-flex h-11 shrink-0 items-center rounded-lg px-4 font-semibold text-white"
          >
            + Add address
          </Link>
        )}
      </div>
      {!canAdd && <p className="text-ink-muted mt-2 text-sm">You've saved the maximum of {MAX_ADDRESSES} addresses.</p>}
      <ul className="mt-4 grid gap-4 sm:grid-cols-2">
        {addresses.map((address) => (
          <AddressCard key={address.id} address={address} />
        ))}
      </ul>
    </div>
  )
}
