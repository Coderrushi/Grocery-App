export function Spinner({ label = 'Loading…', className = '' }) {
  return (
    <div role="status" className={`text-ink-muted flex items-center justify-center gap-3 py-12 ${className}`}>
      <span
        aria-hidden="true"
        className="border-brand-200 border-t-brand-600 size-6 animate-spin rounded-full border-[3px]"
      />
      <span className="text-sm">{label}</span>
    </div>
  )
}
