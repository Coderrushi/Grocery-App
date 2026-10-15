import { useSyncExternalStore } from 'react'

// A tiny global toast store: any code (including mutation callbacks outside components) can call
// toast(...), and <ToastViewport /> renders the messages. No context provider needed.

let toasts = []
const listeners = new Set()
let nextId = 1

function emit() {
  for (const listener of listeners) listener()
}

export function dismissToast(id) {
  toasts = toasts.filter((t) => t.id !== id)
  emit()
}

/** Shows a short message. tone: 'info' | 'success' | 'error'. */
export function toast(message, { tone = 'info', durationMs = 4000 } = {}) {
  const id = nextId++
  toasts = [...toasts.slice(-2), { id, message, tone }] // keep at most 3 on screen
  emit()
  setTimeout(() => dismissToast(id), durationMs)
  return id
}

export function useToasts() {
  return useSyncExternalStore(
    (listener) => {
      listeners.add(listener)
      return () => listeners.delete(listener)
    },
    () => toasts,
  )
}
