import { Link, NavLink } from 'react-router'
import { useCategories } from './catalogApi'

/** Large emoji tiles for the home page: easy to recognise and to tap. */
export function CategoryTiles() {
  const { data: categories } = useCategories()
  if (!categories) return <div className="h-40" aria-hidden="true" />

  return (
    <ul className="grid grid-cols-4 gap-3 sm:grid-cols-4 lg:grid-cols-8">
      {categories.map((c) => (
        <li key={c.id}>
          <Link
            to={`/category/${c.slug}`}
            className="hover:bg-brand-50 flex flex-col items-center gap-2 rounded-xl p-2 text-center"
          >
            <span aria-hidden="true" className="bg-brand-50 grid size-16 place-items-center rounded-2xl text-3xl">
              {c.icon ?? '🛒'}
            </span>
            <span className="text-xs leading-tight font-medium sm:text-sm">{c.name}</span>
          </Link>
        </li>
      ))}
    </ul>
  )
}

/** Horizontally scrolling aisle chips for switching between categories on listing pages. */
export function CategoryChips() {
  const { data: categories } = useCategories()
  if (!categories) return null

  return (
    <nav aria-label="Categories" className="-mx-4 overflow-x-auto px-4 pb-2">
      <ul className="flex w-max gap-2">
        {categories.map((c) => (
          <li key={c.id}>
            <NavLink
              to={`/category/${c.slug}`}
              className={({ isActive }) =>
                `flex h-9 items-center gap-1.5 rounded-full border px-3 text-sm whitespace-nowrap ${
                  isActive
                    ? 'border-brand-600 bg-brand-600 text-white'
                    : 'border-line bg-surface hover:border-brand-600'
                }`
              }
            >
              <span aria-hidden="true">{c.icon}</span>
              {c.name}
            </NavLink>
          </li>
        ))}
      </ul>
    </nav>
  )
}
