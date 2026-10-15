import { useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router'

export function SearchBox({ className = '' }) {
  const [params] = useSearchParams()
  const [value, setValue] = useState(params.get('q') ?? '')
  const navigate = useNavigate()

  const onSubmit = (event) => {
    event.preventDefault()
    const q = value.trim()
    if (q) navigate(`/search?q=${encodeURIComponent(q)}`)
  }

  return (
    <form role="search" onSubmit={onSubmit} className={className}>
      <label className="relative block">
        <span className="sr-only">Search products</span>
        <span
          aria-hidden="true"
          className="text-ink-muted pointer-events-none absolute top-1/2 left-3 -translate-y-1/2"
        >
          🔍
        </span>
        <input
          type="search"
          enterKeyHint="search"
          placeholder="Search for atta, milk, onions…"
          value={value}
          onChange={(e) => setValue(e.target.value)}
          className="border-line bg-canvas focus:border-brand-600 focus:bg-surface h-11 w-full rounded-full border pr-4 pl-10 text-base focus:outline-none"
        />
      </label>
    </form>
  )
}
