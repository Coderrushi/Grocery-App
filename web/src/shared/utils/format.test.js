import { describe, expect, it } from 'vitest'
import { formatCurrency, formatDateTime } from './format'

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
