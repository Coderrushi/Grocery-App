import { config } from '../app/config'
import { CategoryTiles } from '../features/catalog/CategoryNav'
import { ProductGrid } from '../features/catalog/ProductGrid'

export function HomePage() {
  return (
    <div className="space-y-8">
      <section className="rounded-card bg-brand-600 shadow-card relative overflow-hidden px-6 py-8 text-white sm:px-10 sm:py-10">
        <p className="text-sm font-semibold tracking-wide text-white/80 uppercase">Your neighbourhood kirana, online</p>
        <h1 className="mt-2 max-w-lg text-2xl font-bold sm:text-4xl">Fresh groceries delivered to your doorstep</h1>
        <p className="mt-3 max-w-xl text-white/90">
          Order daily essentials from {config.storeName} and we'll bring them straight to your door.
        </p>
        <span aria-hidden="true" className="absolute -right-4 -bottom-6 text-8xl opacity-30 sm:text-9xl">
          🧺
        </span>
      </section>

      <section aria-labelledby="shop-by-category">
        <h2 id="shop-by-category" className="mb-3 text-lg font-bold">
          Shop by category
        </h2>
        <CategoryTiles />
      </section>

      <section aria-labelledby="todays-picks">
        <h2 id="todays-picks" className="mb-3 text-lg font-bold">
          Today's picks
        </h2>
        <ProductGrid params={{ featured: true, pageSize: 10 }} emptyMessage="New products are on their way." />
      </section>
    </div>
  )
}
