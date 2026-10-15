import { NavLink, Outlet } from 'react-router'

const tabs = [
  { to: '/account', label: 'Profile', end: true },
  { to: '/account/addresses', label: 'Addresses' },
]

export function AccountLayout() {
  return (
    <div className="mx-auto max-w-3xl">
      <h1 className="text-2xl font-bold">My account</h1>
      <nav aria-label="Account sections" className="border-line mt-4 flex gap-1 border-b">
        {tabs.map((tab) => (
          <NavLink
            key={tab.to}
            to={tab.to}
            end={tab.end}
            className={({ isActive }) =>
              `-mb-px border-b-2 px-4 py-2.5 text-sm font-semibold ${
                isActive ? 'border-brand-600 text-brand-700' : 'text-ink-muted hover:text-ink border-transparent'
              }`
            }
          >
            {tab.label}
          </NavLink>
        ))}
      </nav>
      <div className="mt-6">
        <Outlet />
      </div>
    </div>
  )
}
