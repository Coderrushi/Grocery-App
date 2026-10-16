import { useEffect, useId, useRef, useState } from 'react'
import { useTheme } from './theme'

const OPTIONS = [
  { value: 'light', label: 'Light', icon: '☀️' },
  { value: 'dark', label: 'Dark', icon: '🌙' },
  { value: 'system', label: 'System', icon: '💻', hint: 'Match this device' },
]

/** Header button with a small menu: Light / Dark / System. */
export function ThemeMenu() {
  const { theme, resolved, setTheme } = useTheme()
  const [open, setOpen] = useState(false)
  const menuId = useId()
  const container = useRef(null)
  const button = useRef(null)

  // Close on outside click or Escape (returning focus to the button).
  useEffect(() => {
    if (!open) return
    const onPointerDown = (event) => {
      if (!container.current?.contains(event.target)) setOpen(false)
    }
    const onKeyDown = (event) => {
      if (event.key === 'Escape') {
        setOpen(false)
        button.current?.focus()
      }
    }
    document.addEventListener('pointerdown', onPointerDown)
    document.addEventListener('keydown', onKeyDown)
    return () => {
      document.removeEventListener('pointerdown', onPointerDown)
      document.removeEventListener('keydown', onKeyDown)
    }
  }, [open])

  const current = OPTIONS.find((o) => o.value === theme)
  const shownIcon = theme === 'system' ? current.icon : resolved === 'dark' ? '🌙' : '☀️'

  return (
    <div ref={container} className="relative">
      <button
        ref={button}
        type="button"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-controls={menuId}
        aria-label={`Theme: ${current.label}`}
        onClick={() => setOpen((o) => !o)}
        className="hover:bg-brand-50 grid size-10 place-items-center rounded-lg text-lg"
      >
        <span aria-hidden="true">{shownIcon}</span>
      </button>
      {open && (
        <ul
          id={menuId}
          role="menu"
          aria-label="Theme"
          className="border-line bg-surface shadow-card absolute right-0 z-50 mt-2 w-48 rounded-xl border p-1"
        >
          {OPTIONS.map((option) => (
            <li key={option.value} role="none">
              <button
                type="button"
                role="menuitemradio"
                aria-checked={theme === option.value}
                autoFocus={theme === option.value}
                onClick={() => {
                  setTheme(option.value)
                  setOpen(false)
                  button.current?.focus()
                }}
                className="hover:bg-brand-50 aria-checked:text-brand-700 flex w-full items-center gap-3 rounded-lg px-3 py-2 text-left text-sm aria-checked:font-semibold"
              >
                <span aria-hidden="true">{option.icon}</span>
                <span className="flex-1">
                  {option.label}
                  {option.hint && <span className="text-ink-muted block text-xs font-normal">{option.hint}</span>}
                </span>
                {theme === option.value && <span aria-hidden="true">✓</span>}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
