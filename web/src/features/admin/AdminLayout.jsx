import { NavLink, Outlet } from 'react-router'

const links = [
  { to: '/admin/orders', label: 'Orders', icon: '🧾' },
  { to: '/admin/products', label: 'Products', icon: '📦' },
  { to: '/admin/categories', label: 'Categories', icon: '🗂️' },
  { to: '/admin/customers', label: 'Customers', icon: '👥' },
]

/** Back-office shell: a sidebar on wide screens, a scrollable tab row on phones. */
export function AdminLayout() {
  return (
    <div className="md:grid md:grid-cols-[12rem_1fr] md:gap-8">
      <nav aria-label="Admin" className="-mx-4 mb-4 overflow-x-auto px-4 md:mx-0 md:mb-0 md:px-0">
        <p className="text-ink-muted mb-2 hidden text-xs font-semibold tracking-wide uppercase md:block">Store admin</p>
        <ul className="flex gap-2 md:flex-col md:gap-1">
          {links.map((link) => (
            <li key={link.to}>
              <NavLink
                to={link.to}
                className={({ isActive }) =>
                  `flex h-10 items-center gap-2 rounded-lg px-3 text-sm font-semibold whitespace-nowrap ${
                    isActive ? 'bg-brand-600 text-white' : 'hover:bg-brand-50'
                  }`
                }
              >
                <span aria-hidden="true">{link.icon}</span>
                {link.label}
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>
      <div className="min-w-0">
        <Outlet />
      </div>
    </div>
  )
}
