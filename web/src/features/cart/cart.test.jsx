import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { anonymous, customer, emptyCart, mockApi, renderApp, signedInAs } from '../../test/renderApp'
import { guestCart } from './guestCart'

const milk = {
  id: 1,
  name: 'Taaza Toned Milk',
  brand: 'Amul',
  unitLabel: '500 ml',
  price: 28,
  mrp: 28,
  discountPercent: 0,
  imageUrl: null,
  categorySlug: 'dairy',
  categoryIcon: '🥛',
  stockStatus: 'InStock',
  onlyLeft: null,
  maxQuantity: 2,
}

const catalogHandlers = {
  'GET /catalog/categories': () => [200, []],
  'GET /catalog/products': () => [200, { items: [milk], page: 1, pageSize: 24, totalCount: 1, totalPages: 1 }],
}

/** A priced cart like the API returns, for the given [product, quantity] pairs. */
function pricedCart(pairs, extra = {}) {
  const lines = pairs.map(([p, quantity]) => ({
    productId: p.id,
    name: p.name,
    brand: p.brand,
    unitLabel: p.unitLabel,
    imageUrl: null,
    categoryIcon: p.categoryIcon,
    unitPrice: p.price,
    unitMrp: p.mrp,
    quantity,
    lineTotal: p.price * quantity,
    maxQuantity: p.maxQuantity,
    stockStatus: 'InStock',
    issue: null,
    ...extra.lineOverrides,
  }))
  const subtotal = lines.reduce((s, l) => s + l.lineTotal, 0)
  return {
    ...emptyCart,
    lines,
    totals: {
      ...emptyCart.totals,
      itemCount: lines.reduce((s, l) => s + l.quantity, 0),
      subtotal,
      mrpTotal: subtotal,
      deliveryFee: 25,
      total: subtotal + 25,
      amountToFreeDelivery: 299 - subtotal,
      meetsMinimumOrder: subtotal >= 99,
    },
    ...extra.cart,
  }
}

describe('guest cart', () => {
  it('adds instantly, keeps the list in the browser, and asks the API for prices', async () => {
    const user = userEvent.setup()
    const calls = mockApi({
      ...anonymous,
      ...catalogHandlers,
      'POST /cart/preview': (body) => [200, pricedCart(body.items.map((i) => [milk, i.quantity]))],
    })
    renderApp('/search?q=milk')

    await user.click(await screen.findByRole('button', { name: 'Add Taaza Toned Milk to cart' }))

    expect(screen.getByRole('group', { name: 'Quantity of Taaza Toned Milk' })).toHaveTextContent('1')
    expect(JSON.parse(localStorage.getItem('qk_guest_cart'))).toEqual([{ productId: 1, quantity: 1 }])
    expect(await screen.findByRole('link', { name: 'Cart, 1 item' })).toBeInTheDocument()
    expect(calls.find((c) => c.path === '/cart/preview').body).toEqual({ items: [{ productId: 1, quantity: 1 }] })
  })

  it('stops at the maximum a customer can buy', async () => {
    const user = userEvent.setup()
    mockApi({
      ...anonymous,
      ...catalogHandlers,
      'POST /cart/preview': (body) => [200, pricedCart(body.items.map((i) => [milk, i.quantity]))],
    })
    renderApp('/search?q=milk')

    await user.click(await screen.findByRole('button', { name: 'Add Taaza Toned Milk to cart' }))
    const increase = screen.getByRole('button', { name: 'Increase Taaza Toned Milk' })
    await user.click(increase)

    expect(screen.getByRole('group', { name: 'Quantity of Taaza Toned Milk' })).toHaveTextContent('2')
    expect(increase).toBeDisabled() // maxQuantity is 2
  })

  it('cart page offers sign-in to place the order', async () => {
    guestCart.setQuantity(1, 2)
    mockApi({ ...anonymous, 'POST /cart/preview': () => [200, pricedCart([[milk, 2]])] })
    renderApp('/cart')

    expect(await screen.findByRole('link', { name: 'Sign in to place your order' })).toHaveAttribute(
      'href',
      '/login?returnTo=%2Fcart',
    )
  })

  it('merges into the saved cart after signing in', async () => {
    const user = userEvent.setup()
    guestCart.setQuantity(1, 2)
    let signedIn = false
    const calls = mockApi({
      'GET /auth/me': () => (signedIn ? [200, customer] : [401, { status: 401 }]),
      'POST /cart/preview': () => [200, pricedCart([[milk, 2]])],
      'POST /auth/login': () => {
        signedIn = true
        return [200, customer]
      },
      'POST /cart/merge': () => [200, pricedCart([[milk, 2]])],
      'GET /cart': () => [200, pricedCart([[milk, 2]])],
    })
    renderApp('/login?returnTo=%2Fcart')

    await user.type(await screen.findByLabelText('Email or mobile number'), 'asha@example.com')
    await user.type(screen.getByLabelText('Password'), 'secret-password')
    await user.click(screen.getByRole('button', { name: 'Sign in' }))

    await waitFor(() => expect(calls.some((c) => c.path === '/cart/merge')).toBe(true))
    expect(calls.find((c) => c.path === '/cart/merge').body).toEqual({ items: [{ productId: 1, quantity: 2 }] })
    expect(await screen.findByText('We kept the items you added before signing in.')).toBeInTheDocument()
    expect(localStorage.getItem('qk_guest_cart')).toBeNull()
  })
})

describe('signed-in cart', () => {
  it('saves changes on the server and shows the API error if a change is refused', async () => {
    const user = userEvent.setup()
    const calls = mockApi({
      ...signedInAs(customer),
      ...catalogHandlers,
      'PUT /cart/items/1': () => [
        422,
        { status: 422, title: 'Business rule violated', detail: 'Only 1 available right now.' },
      ],
    })
    renderApp('/search?q=milk')

    await user.click(await screen.findByRole('button', { name: 'Add Taaza Toned Milk to cart' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Only 1 available right now.')
    expect(calls.find((c) => c.method === 'PUT').body).toEqual({ quantity: 1 })
    expect(screen.getByRole('button', { name: 'Add Taaza Toned Milk to cart' })).toBeInTheDocument()
  })

  it('cart page shows the bill and fixes an item that exceeds stock', async () => {
    const user = userEvent.setup()
    const tooMany = pricedCart([[milk, 3]], {
      lineOverrides: { issue: 'ExceedsAvailable', maxQuantity: 2 },
      cart: { hasIssues: true, canCheckout: false },
    })
    const calls = mockApi({
      ...signedInAs(customer),
      'GET /cart': () => [200, tooMany],
      'PUT /cart/items/1': () => [200, pricedCart([[milk, 2]])],
    })
    renderApp('/cart')

    expect(await screen.findByText('Only 2 available. Reduce the quantity to continue.')).toBeInTheDocument()
    expect(screen.getByText('Please fix the items marked above to continue.')).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Change to 2' }))

    await waitFor(() => expect(screen.queryByText(/Reduce the quantity/)).not.toBeInTheDocument())
    expect(calls.find((c) => c.method === 'PUT').body).toEqual({ quantity: 2 })
    const bill = screen.getByRole('heading', { name: 'Bill details' }).closest('section')
    expect(within(bill).getByText('₹81.00')).toBeInTheDocument() // 56 + 25 delivery
    expect(within(bill).getByText(/more for FREE delivery/)).toBeInTheDocument()
  })

  it('shows a friendly empty state', async () => {
    mockApi(signedInAs(customer))
    renderApp('/cart')

    expect(await screen.findByRole('heading', { name: 'Your cart is empty' })).toBeInTheDocument()
  })
})

describe('guestCart storage', () => {
  it('ignores corrupted data', () => {
    localStorage.setItem('qk_guest_cart', '{not json')
    // Re-reading happens on the storage event (another tab), which is the path that sees external data.
    window.dispatchEvent(new StorageEvent('storage', { key: 'qk_guest_cart' }))
    expect(guestCart.getItems()).toEqual([])
  })
})
