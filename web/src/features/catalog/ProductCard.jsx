import { Link } from 'react-router'
import { DiscountRibbon, PriceTag, ProductImage, StockBadge } from './ProductBits'
import { productUrl } from './productUrl'

export function ProductCard({ product }) {
  const soldOut = product.stockStatus === 'OutOfStock'
  return (
    <li className="rounded-card border-line bg-surface shadow-card relative flex flex-col border p-3">
      <div className={`relative ${soldOut ? 'opacity-50 grayscale' : ''}`}>
        <ProductImage product={product} />
        <DiscountRibbon product={product} />
      </div>
      <div className="mt-2 flex flex-1 flex-col">
        {product.brand && <p className="text-ink-muted text-xs">{product.brand}</p>}
        <h3 className="line-clamp-2 text-sm leading-snug font-medium">
          {/* The whole card is clickable via this link's ::after overlay. */}
          <Link to={productUrl(product)} className="after:absolute after:inset-0 after:content-['']">
            {product.name}
          </Link>
        </h3>
        <p className="text-ink-muted mt-0.5 text-xs">{product.unitLabel}</p>
        <div className="mt-auto pt-2">
          <PriceTag product={product} />
          <StockBadge product={product} />
        </div>
        {/* Phase 4: "Add" button / quantity stepper goes here. */}
      </div>
    </li>
  )
}
