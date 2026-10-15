import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'
import { guestCart } from '../features/cart/guestCart'

afterEach(() => {
  cleanup()
  guestCart.clear() // module-level state would otherwise leak between tests
})
