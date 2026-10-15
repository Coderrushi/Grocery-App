import { useSearchParams } from 'react-router'

/**
 * Sort + "in stock only" controls. Their state lives in the URL (?sort=...&inStock=1), so a filtered
 * page can be shared, bookmarked, and survives the back button.
 */
export function useListingParams() {
  const [searchParams, setSearchParams] = useSearchParams()
  const sort = searchParams.get('sort') ?? 'Relevance'
  const inStockOnly = searchParams.get('inStock') === '1'

  const update = (changes) =>
    setSearchParams(
      (current) => {
        const next = new URLSearchParams(current)
        for (const [key, value] of Object.entries(changes)) {
          if (value) next.set(key, value)
          else next.delete(key)
        }
        return next
      },
      { replace: true },
    )

  return {
    sort,
    inStockOnly,
    setSort: (value) => update({ sort: value === 'Relevance' ? null : value }),
    setInStockOnly: (value) => update({ inStock: value ? '1' : null }),
  }
}
