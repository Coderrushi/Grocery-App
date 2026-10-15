import { useState } from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { apiClient } from '../../shared/api/apiClient'
import { Button } from '../../shared/ui/Button'
import { ErrorAlert } from '../../shared/ui/Alert'
import { Spinner } from '../../shared/ui/Spinner'
import { formatDateTime } from '../../shared/utils/format'

function useCustomers({ search, page }) {
  return useQuery({
    queryKey: ['admin', 'customers', { search, page }],
    queryFn: ({ signal }) => {
      const params = new URLSearchParams({ page, pageSize: 20 })
      if (search) params.set('search', search)
      return apiClient.get(`/admin/customers?${params}`, { signal })
    },
    placeholderData: keepPreviousData, // keep the table on screen while the next page loads
  })
}

export function AdminCustomersPage() {
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const { data, isPending, isFetching, error } = useCustomers({ search, page })

  const onSearch = (event) => {
    event.preventDefault()
    setSearch(searchInput.trim())
    setPage(1)
  }

  return (
    <div>
      <h1 className="text-2xl font-bold">Customers</h1>

      <form onSubmit={onSearch} role="search" className="mt-4 flex gap-2">
        <input
          type="search"
          aria-label="Search customers"
          placeholder="Name, email or mobile number"
          value={searchInput}
          onChange={(e) => setSearchInput(e.target.value)}
          className="border-line bg-surface focus:border-brand-600 h-11 w-full max-w-sm rounded-lg border px-3 focus:outline-none"
        />
        <Button type="submit" variant="secondary">
          Search
        </Button>
      </form>

      {error && (
        <div className="mt-4">
          <ErrorAlert error={error} title="Couldn't load customers" />
        </div>
      )}
      {isPending && <Spinner />}

      {data && (
        <>
          <div className="rounded-card border-line bg-surface mt-4 overflow-x-auto border">
            <table className={`w-full text-left text-sm ${isFetching ? 'opacity-60' : ''}`}>
              <thead className="bg-canvas text-ink-muted">
                <tr>
                  <th className="px-4 py-3 font-semibold">Name</th>
                  <th className="px-4 py-3 font-semibold">Mobile</th>
                  <th className="px-4 py-3 font-semibold">Email</th>
                  <th className="px-4 py-3 font-semibold">Addresses</th>
                  <th className="px-4 py-3 font-semibold">Registered</th>
                </tr>
              </thead>
              <tbody className="divide-line divide-y">
                {data.items.map((c) => (
                  <tr key={c.id}>
                    <td className="px-4 py-3 font-medium">{c.fullName}</td>
                    <td className="px-4 py-3">{c.phoneNumber}</td>
                    <td className="px-4 py-3">{c.email}</td>
                    <td className="px-4 py-3">{c.addressCount}</td>
                    <td className="px-4 py-3 whitespace-nowrap">{formatDateTime(c.registeredAtUtc)}</td>
                  </tr>
                ))}
                {data.items.length === 0 && (
                  <tr>
                    <td colSpan={5} className="text-ink-muted px-4 py-8 text-center">
                      No customers found.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>

          <div className="mt-4 flex items-center justify-between text-sm">
            <span className="text-ink-muted">
              {data.totalCount} customer{data.totalCount === 1 ? '' : 's'}
            </span>
            <div className="flex items-center gap-2">
              <Button size="sm" variant="secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                Previous
              </Button>
              <span>
                Page {data.page} of {Math.max(1, data.totalPages)}
              </span>
              <Button
                size="sm"
                variant="secondary"
                disabled={page >= data.totalPages}
                onClick={() => setPage((p) => p + 1)}
              >
                Next
              </Button>
            </div>
          </div>
        </>
      )}
    </div>
  )
}
