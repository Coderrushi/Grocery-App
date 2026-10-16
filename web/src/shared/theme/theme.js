import { useSyncExternalStore } from 'react'

/**
 * Colour theme: the shopper's choice ('light' | 'dark' | 'system') is saved in localStorage, and the
 * RESOLVED theme ('light' | 'dark') is written to <html data-theme="…">. index.css switches every
 * design token on that attribute, so components need no theme logic of their own.
 *
 * index.html runs the same resolution inline before first paint, so there is no light flash on load.
 * Keep STORAGE_KEY and the resolution rule in sync with that script.
 */

export const THEMES = ['light', 'dark', 'system']
const STORAGE_KEY = 'qk_theme'
const META_COLORS = { light: '#16843f', dark: '#0f1412' } // browser UI (address bar) colour

const darkQuery =
  typeof window !== 'undefined' && window.matchMedia ? window.matchMedia('(prefers-color-scheme: dark)') : null

const listeners = new Set()
let preference = readPreference()

function readPreference() {
  try {
    const stored = localStorage.getItem(STORAGE_KEY)
    return THEMES.includes(stored) ? stored : 'system'
  } catch {
    return 'system' // storage blocked (private mode, site data disabled)
  }
}

export function resolveTheme(pref) {
  if (pref === 'system') return darkQuery?.matches ? 'dark' : 'light'
  return pref
}

function apply() {
  const resolved = resolveTheme(preference)
  const root = document.documentElement
  root.dataset.theme = resolved
  root.style.colorScheme = resolved // native controls, scrollbars and form widgets follow too
  document.querySelector('meta[name="theme-color"]')?.setAttribute('content', META_COLORS[resolved])
  for (const listener of listeners) listener()
}

export function setTheme(next) {
  if (!THEMES.includes(next)) return
  preference = next
  try {
    if (next === 'system') localStorage.removeItem(STORAGE_KEY)
    else localStorage.setItem(STORAGE_KEY, next)
  } catch {
    // Not persisted, but still applied for this visit.
  }
  apply()
}

// "System" follows the OS live, e.g. when the phone switches to night mode at sunset.
darkQuery?.addEventListener?.('change', () => {
  if (preference === 'system') apply()
})

// Another open tab changed the theme.
if (typeof window !== 'undefined') {
  window.addEventListener('storage', (event) => {
    if (event.key === STORAGE_KEY) {
      preference = readPreference()
      apply()
    }
  })
}

const snapshot = () => `${preference}:${resolveTheme(preference)}`

/** { theme: the saved choice, resolved: what is actually showing, setTheme } */
export function useTheme() {
  const value = useSyncExternalStore((listener) => {
    listeners.add(listener)
    return () => listeners.delete(listener)
  }, snapshot)
  const [theme, resolved] = value.split(':')
  return { theme, resolved, setTheme }
}
