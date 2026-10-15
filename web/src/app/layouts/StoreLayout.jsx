import { Link, Outlet } from 'react-router'
import { config } from '../config'

const currentYear = new Date().getFullYear()

export function StoreLayout() {
  return (
    <div className="flex min-h-dvh flex-col">
      <a
        href="#main"
        className="focus:bg-surface sr-only focus:not-sr-only focus:absolute focus:top-2 focus:left-2 focus:z-50 focus:rounded focus:px-3 focus:py-2"
      >
        Skip to content
      </a>

      <header className="border-line bg-surface/95 sticky top-0 z-40 border-b backdrop-blur">
        <div className="mx-auto flex h-16 max-w-6xl items-center justify-between gap-4 px-4">
          <Link to="/" className="text-brand-700 flex items-center gap-2 text-lg font-bold">
            <span aria-hidden="true" className="bg-brand-600 grid size-9 place-items-center rounded-lg text-white">
              🛒
            </span>
            {config.storeName}
          </Link>
          {/* Search, account and cart arrive in Phases 2–4. */}
        </div>
      </header>

      <main id="main" className="mx-auto w-full max-w-6xl flex-1 px-4 py-6">
        <Outlet />
      </main>

      <footer className="border-line bg-surface border-t">
        <div className="text-ink-muted mx-auto max-w-6xl px-4 py-6 text-sm">
          © {currentYear} {config.storeName}. Fresh groceries delivered to your door.
        </div>
      </footer>
    </div>
  )
}
