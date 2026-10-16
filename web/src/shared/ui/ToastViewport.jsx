import { dismissToast, useToasts } from './toast'

const tones = {
  info: 'bg-ink text-canvas', // inverts with the theme: dark on light, light on dark
  success: 'bg-brand-600 text-white',
  error: 'bg-danger-600 text-white',
}

/** Bottom-centre on phones (above the cart bar), bottom-right on desktop. */
export function ToastViewport() {
  const toasts = useToasts()
  return (
    <div
      aria-live="polite"
      className="pointer-events-none fixed inset-x-0 bottom-20 z-50 flex flex-col items-center gap-2 px-4 md:right-4 md:bottom-4 md:left-auto md:items-end"
    >
      {toasts.map((t) => (
        <div
          key={t.id}
          role={t.tone === 'error' ? 'alert' : 'status'}
          className={`pointer-events-auto flex max-w-sm items-start gap-3 rounded-lg px-4 py-3 text-sm shadow-lg ${tones[t.tone]}`}
        >
          <span className="flex-1">{t.message}</span>
          <button
            type="button"
            onClick={() => dismissToast(t.id)}
            aria-label="Dismiss"
            className="opacity-80 hover:opacity-100"
          >
            ✕
          </button>
        </div>
      ))}
    </div>
  )
}
