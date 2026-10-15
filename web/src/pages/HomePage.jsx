import { SystemStatus } from '../features/system/SystemStatus'
import { config } from '../app/config'

export function HomePage() {
  return (
    <section className="rounded-card bg-brand-600 shadow-card px-6 py-10 text-white sm:px-10">
      <p className="text-brand-100 text-sm font-semibold tracking-wide uppercase">Coming soon</p>
      <h1 className="mt-2 text-3xl font-bold sm:text-4xl">{config.storeName}</h1>
      <p className="text-brand-50 mt-3 max-w-xl">Daily essentials and fresh groceries, delivered to your doorstep.</p>
      {import.meta.env.DEV && (
        <div className="text-ink mt-6">
          <SystemStatus />
        </div>
      )}
    </section>
  )
}
