const tones = {
  error: 'border-danger-600/30 bg-danger-50 text-danger-700',
  success: 'border-brand-200 bg-brand-50 text-brand-900',
  info: 'border-line bg-surface text-ink',
}

export function Alert({ tone = 'info', title, children }) {
  return (
    <div role={tone === 'error' ? 'alert' : 'status'} className={`rounded-lg border p-4 text-sm ${tones[tone]}`}>
      {title && <p className="font-semibold">{title}</p>}
      {children && <div className={title ? 'mt-1' : undefined}>{children}</div>}
    </div>
  )
}

/** Renders an ApiError (or any error) in a user-friendly way, including the support reference. */
export function ErrorAlert({ error, title = 'Something went wrong' }) {
  if (!error) return null
  return (
    <Alert tone="error" title={title}>
      <p>{error.detail ?? error.message}</p>
      {error.traceId && <p className="mt-1 text-xs opacity-75">Reference: {error.traceId}</p>}
    </Alert>
  )
}
