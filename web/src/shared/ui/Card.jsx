export function Card({ title, description, actions, children, className = '' }) {
  return (
    <section className={`rounded-card border-line bg-surface shadow-card border p-5 sm:p-6 ${className}`}>
      {(title || actions) && (
        <header className="mb-4 flex items-start justify-between gap-4">
          <div>
            {title && <h2 className="text-lg font-semibold">{title}</h2>}
            {description && <p className="text-ink-muted mt-1 text-sm">{description}</p>}
          </div>
          {actions}
        </header>
      )}
      {children}
    </section>
  )
}
