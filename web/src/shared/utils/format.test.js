import { describe, expect, it } from 'vitest'
import { formatCompactCurrency, formatCurrency, formatDateTime, formatDay } from './format'

describe('formatCurrency', () => {
  it('formats rupees with Indian digit grouping', () => {
    expect(formatCurrency(123456.5)).toBe('₹1,23,456.50')
  })

  it('accepts decimal strings from the API', () => {
    expect(formatCurrency('49.9')).toBe('₹49.90')
  })
})

describe('formatDateTime', () => {
  it('shows UTC timestamps in IST', () => {
    // 08:30 UTC is 14:00 IST
    expect(formatDateTime('2026-10-15T08:30:00Z')).toMatch(/2:00\s?pm/i)
  })
})

describe('formatCompactCurrency', () => {
  it('uses Indian thousands, lakhs and crores', () => {
    expect([950, 1500, 250000, 12500000].map(formatCompactCurrency)).toEqual(['₹950', '₹1.5K', '₹2.5L', '₹1.3Cr'])
  })
})

describe('formatDay', () => {
  it('formats a calendar day without shifting it by a time zone', () => {
    expect(formatDay('2026-10-05')).toBe('5 Oct')
    expect(formatDay('2026-10-05', { long: true })).toBe('Mon, 5 Oct, 2026')
  })
})
