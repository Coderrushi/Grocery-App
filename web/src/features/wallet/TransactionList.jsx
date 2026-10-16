import { Button } from '../../shared/ui/Button'
import { ErrorAlert } from '../../shared/ui/Alert'
import { Spinner } from '../../shared/ui/Spinner'
import { formatCurrency, formatDateTime } from '../../shared/utils/format'
import { useWalletTransactions } from './walletApi'
import { TYPE_LABELS } from './transactionTypes'

/** The wallet ledger, newest first. Pass `userId` when an admin is viewing a customer's wallet. */
export function TransactionList({ userId }) {
  const { data, error, isPending, fetchNextPage, hasNextPage, isFetchingNextPage } = useWalletTransactions(userId)

  if (isPending) return <Spinner label="Loading transactions…" />
  if (error) return <ErrorAlert error={error} title="Couldn't load transactions" />

  const transactions = data.pages.flatMap((p) => p.items)
  if (transactions.length === 0) {
    return <p className="text-ink-muted py-6 text-center text-sm">No transactions yet.</p>
  }

  return (
    <div>
      <ul className="divide-line divide-y">
        {transactions.map((t) => {
          const type = TYPE_LABELS[t.type] ?? { label: t.type, icon: '•' }
          const credit = t.amount > 0
          return (
            <li key={t.id} className="flex items-start gap-3 py-3">
              <span aria-hidden="true" className="bg-canvas grid size-9 shrink-0 place-items-center rounded-full">
                {type.icon}
              </span>
              <div className="min-w-0 flex-1">
                <p className="font-medium">{type.label}</p>
                <p className="text-ink-muted truncate text-xs">{t.description}</p>
                <p className="text-ink-muted text-xs">{formatDateTime(t.createdAtUtc)}</p>
              </div>
              <div className="text-right">
                <p className={`font-semibold ${credit ? 'text-success-600' : 'text-ink'}`}>
                  {credit ? '+' : '−'}
                  {formatCurrency(Math.abs(t.amount))}
                </p>
                <p className="text-ink-muted text-xs">Balance {formatCurrency(t.balanceAfter)}</p>
              </div>
            </li>
          )
        })}
      </ul>
      {hasNextPage && (
        <div className="mt-3 text-center">
          <Button variant="secondary" size="sm" onClick={() => fetchNextPage()} disabled={isFetchingNextPage}>
            {isFetchingNextPage ? 'Loading…' : 'Show older'}
          </Button>
        </div>
      )}
    </div>
  )
}
