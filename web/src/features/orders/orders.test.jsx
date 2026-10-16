import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { customer, emptyCart, mockApi, renderApp, signedInAs } from '../../test/renderApp'

const address = (overrides) => ({
  id: 1,
  label: 'Home',
  recipientName: 'Asha Verma',
  phoneNumber: '9876543210',
  line1: 'Flat 4B',
  line2: null,
  landmark: null,
  city: 'Pune',
  state: 'Maharashtra',
  pincode: '411038',
  isDefault: true,
  ...overrides,
})

const cart = {
  ...emptyCart,
  lines: [
    {
      productId: 5,
      name: 'Fresh Paneer',
      brand: 'Amul',
      unitLabel: '200 g',
      imageUrl: null,
      categoryIcon: '🥛',
      unitPrice: 100,
      unitMrp: 110,
      quantity: 2,
      lineTotal: 200,
      maxQuantity: 10,
      stockStatus: 'InStock',
      issue: null,
    },
  ],
  totals: {
    itemCount: 2,
    subtotal: 200,
    mrpTotal: 220,
    savings: 20,
    deliveryFee: 25,
    total: 225,
    amountToFreeDelivery: 99,
    minimumOrderAmount: 99,
    meetsMinimumOrder: true,
  },
  canCheckout: true,
}

const summary = (overrides) => ({
  cart,
  addresses: [{ address: address(), isServiceable: true }],
  walletBalance: 1000,
  hasEnoughBalance: true,
  ...overrides,
})

const placedOrder = {
  id: 42,
  orderNumber: 'QK-000042',
  status: 'Placed',
  paymentMethod: 'Wallet',
  paymentStatus: 'Paid',
  placedAtUtc: '2026-10-15T08:30:00Z',
  items: [
    {
      productId: 5,
      name: 'Fresh Paneer',
      brand: 'Amul',
      unitLabel: '200 g',
      unitPrice: 100,
      unitMrp: 110,
      quantity: 2,
      lineTotal: 200,
    },
  ],
  subtotal: 200,
  mrpTotal: 220,
  savings: 20,
  deliveryFee: 25,
  total: 225,
  deliveryAddress: address(),
  deliveryInstructions: 'Ring twice',
  cancellationReason: null,
  timeline: [
    { fromStatus: null, toStatus: 'Placed', note: null, changedBy: 'You', changedAtUtc: '2026-10-15T08:30:00Z' },
  ],
  canCancel: true,
}

describe('checkout', () => {
  it('places the order with the total the customer saw and shows the confirmation', async () => {
    const user = userEvent.setup()
    const calls = mockApi({
      ...signedInAs(customer),
      'GET /checkout': () => [200, summary()],
      'POST /orders': () => [201, placedOrder],
      'GET /orders/42': () => [200, placedOrder],
    })
    const { router } = renderApp('/checkout')

    expect(await screen.findByRole('radio')).toBeChecked() // default address preselected
    await user.type(screen.getByLabelText('Delivery instructions'), 'Ring twice')
    await user.click(screen.getByRole('button', { name: 'Pay ₹225.00 & place order' }))

    await waitFor(() => expect(router.state.location.pathname).toBe('/account/orders/42'))
    expect(await screen.findByText('🎉 Order placed!')).toBeInTheDocument()
    const body = calls.find((c) => c.method === 'POST' && c.path === '/orders').body
    expect(body).toMatchObject({ addressId: 1, deliveryInstructions: 'Ring twice', expectedTotal: 225 })
    expect(body.checkoutKey).toBeTruthy()
  })

  it('blocks payment when the wallet is short and links to add money', async () => {
    mockApi({
      ...signedInAs(customer),
      'GET /checkout': () => [200, summary({ walletBalance: 100, hasEnoughBalance: false })],
    })
    renderApp('/checkout')

    expect(await screen.findByText(/Add at least ₹125.00/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Add money' })).toHaveAttribute('href', '/account/wallet')
    expect(screen.getByRole('button', { name: /place order/ })).toBeDisabled()
  })

  it('does not allow addresses outside the delivery area', async () => {
    mockApi({
      ...signedInAs(customer),
      'GET /checkout': () => [
        200,
        summary({
          addresses: [
            { address: address({ id: 1, pincode: '560001', isDefault: true }), isServiceable: false },
            { address: address({ id: 2, label: 'Work', isDefault: false }), isServiceable: true },
          ],
        }),
      ],
    })
    renderApp('/checkout')

    const [far, near] = await screen.findAllByRole('radio')
    expect(far).toBeDisabled()
    expect(near).toBeChecked() // the serviceable one is chosen instead of the unserviceable default
    expect(screen.getByText("We don't deliver to this PIN code yet.")).toBeInTheDocument()
  })

  it('reloads the checkout when prices changed and explains why', async () => {
    const user = userEvent.setup()
    let loads = 0
    mockApi({
      ...signedInAs(customer),
      'GET /checkout': () => {
        loads += 1
        return [200, loads === 1 ? summary() : summary({ cart: { ...cart, totals: { ...cart.totals, total: 245 } } })]
      },
      'POST /orders': () => [
        409,
        { status: 409, title: 'Conflict', detail: 'Prices have changed since you opened checkout.' },
      ],
    })
    renderApp('/checkout')

    await user.click(await screen.findByRole('button', { name: 'Pay ₹225.00 & place order' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Prices have changed')
    expect(await screen.findByRole('button', { name: 'Pay ₹245.00 & place order' })).toBeInTheDocument()
  })

  it('cart page links to checkout when the cart is ready', async () => {
    mockApi({ ...signedInAs(customer), 'GET /cart': () => [200, cart] })
    renderApp('/cart')

    expect(await screen.findByRole('link', { name: 'Proceed to checkout' })).toHaveAttribute('href', '/checkout')
  })
})

describe('my orders', () => {
  it('cancels a placed order and shows the refund', async () => {
    const user = userEvent.setup()
    window.confirm = () => true
    const calls = mockApi({
      ...signedInAs(customer),
      'GET /orders/42': () => [200, placedOrder],
      'POST /orders/42/cancel': () => [
        200,
        {
          ...placedOrder,
          status: 'Cancelled',
          paymentStatus: 'Refunded',
          cancellationReason: 'Cancelled by customer',
          canCancel: false,
          timeline: [
            ...placedOrder.timeline,
            {
              fromStatus: 'Placed',
              toStatus: 'Cancelled',
              note: 'Cancelled by customer',
              changedBy: 'You',
              changedAtUtc: '2026-10-15T08:35:00Z',
            },
          ],
        },
      ],
    })
    renderApp('/account/orders/42')

    expect(await screen.findByRole('list', { name: 'Order progress' })).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Cancel order' }))

    expect(await screen.findByText('This order was cancelled')).toBeInTheDocument()
    expect(screen.getByText('₹225.00 has been refunded to your wallet.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Cancel order' })).not.toBeInTheDocument()
    expect(calls.some((c) => c.method === 'POST' && c.path === '/orders/42/cancel')).toBe(true)
  })
})

describe('admin order handling', () => {
  const admin = { ...customer, roles: ['Admin'] }
  const adminDetail = (order, allowed, version) => ({
    order,
    customer: { id: 7, fullName: 'Asha Verma', email: 'asha@example.com', phoneNumber: '9876543210' },
    allowedNextStatuses: allowed,
    version,
  })

  it('moves the order forward with the version it was shown', async () => {
    const user = userEvent.setup()
    const calls = mockApi({
      ...signedInAs(admin),
      'GET /admin/orders/42': () => [200, adminDetail(placedOrder, ['Confirmed', 'Cancelled'], 0)],
      'POST /admin/orders/42/status': (body) => [
        200,
        adminDetail({ ...placedOrder, status: body.status }, ['Packed', 'Cancelled'], 1),
      ],
    })
    renderApp('/admin/orders/42')

    expect(await screen.findByRole('link', { name: /9876543210/ })).toHaveAttribute('href', 'tel:+919876543210')
    await user.click(screen.getByRole('button', { name: /Confirm order/ }))

    expect(await screen.findByRole('button', { name: /Mark as packed/ })).toBeInTheDocument()
    expect(calls.find((c) => c.method === 'POST').body).toEqual({ status: 'Confirmed', note: null, version: 0 })
  })

  it('cancelling needs a reason the customer will see', async () => {
    const user = userEvent.setup()
    const calls = mockApi({
      ...signedInAs(admin),
      'GET /admin/orders/42': () => [200, adminDetail(placedOrder, ['Confirmed', 'Cancelled'], 3)],
      'POST /admin/orders/42/status': (body) => [
        200,
        adminDetail({ ...placedOrder, status: 'Cancelled', cancellationReason: body.note }, [], 4),
      ],
    })
    renderApp('/admin/orders/42')

    await user.click(await screen.findByRole('button', { name: 'Cancel order…' }))
    const confirm = screen.getByRole('button', { name: 'Cancel and refund' })
    expect(confirm).toBeDisabled()

    await user.type(screen.getByLabelText(/Reason/), 'Paneer out of stock')
    await user.click(confirm)

    const banner = await screen.findByText('Cancelled', { selector: 'p' })
    expect(within(banner.closest('[role="alert"]')).getByText(/Paneer out of stock/)).toBeInTheDocument()
    expect(calls.find((c) => c.method === 'POST').body).toEqual({
      status: 'Cancelled',
      note: 'Paneer out of stock',
      version: 3,
    })
  })
})
