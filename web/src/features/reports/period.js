/**
 * Report periods are Indian calendar days as "yyyy-mm-dd" strings, the same format the API uses.
 * Arithmetic happens on UTC dates so the browser's own time zone can never shift a day.
 */

const istDate = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Kolkata' }) // en-CA formats as yyyy-mm-dd

/** Today's date in India, whatever time zone the browser is in. */
export function istToday(now = new Date()) {
  return istDate.format(now)
}

const toDate = (day) => new Date(`${day}T00:00:00Z`)
const toDay = (date) => date.toISOString().slice(0, 10)

export function addDays(day, count) {
  const date = toDate(day)
  date.setUTCDate(date.getUTCDate() + count)
  return toDay(date)
}

export const PRESETS = [
  { id: '7d', label: 'Last 7 days' },
  { id: '30d', label: 'Last 30 days' },
  { id: '90d', label: 'Last 90 days' },
  { id: 'month', label: 'This month' },
  { id: 'last-month', label: 'Last month' },
]

export const DEFAULT_PRESET = '30d'

/** { from, to } for a preset, relative to `today`. */
export function presetPeriod(id, today = istToday()) {
  switch (id) {
    case '7d':
      return { from: addDays(today, -6), to: today }
    case '90d':
      return { from: addDays(today, -89), to: today }
    case 'month':
      return { from: `${today.slice(0, 8)}01`, to: today }
    case 'last-month': {
      const lastDay = addDays(`${today.slice(0, 8)}01`, -1)
      return { from: `${lastDay.slice(0, 8)}01`, to: lastDay }
    }
    default:
      return { from: addDays(today, -29), to: today }
  }
}

/** Which preset (if any) a period matches, for highlighting the picker. */
export function matchPreset({ from, to }, today = istToday()) {
  return PRESETS.find((p) => {
    const period = presetPeriod(p.id, today)
    return period.from === from && period.to === to
  })?.id
}
