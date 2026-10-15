import { describe, expect, it } from 'vitest'
import { normalizeIndianMobile } from './india'

describe('normalizeIndianMobile', () => {
  it.each([
    ['9876543210', '9876543210'],
    ['+91 98765 43210', '9876543210'],
    ['+91-98765-43210', '9876543210'],
    ['09876543210', '9876543210'],
    ['919876543210', '9876543210'],
    ['98765', '98765'],
  ])('%s -> %s', (input, expected) => {
    expect(normalizeIndianMobile(input)).toBe(expected)
  })
})
