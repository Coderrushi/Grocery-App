import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { customer, mockApi, renderApp, signedInAs } from '../../test/renderApp'

const limits = { minTopUp: 10, maxTopUp: 10000, maxBalance: 20000 }
const noTransactions = () => [200, { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 }]

describe('customer wallet', () => {
  it('shows the balance and the history', async () => {
    mockApi({
      ...signedInAs(customer),
      'GET /wallet': () => [200, { balance: 750.5, ...limits }],
      'GET /wallet/transactions': () => [
        200,
        {
          items: [
            {
              id: 2,
              type: 'AdminCredit',
              amount: 250.5,
              balanceAfter: 750.5,
              description: 'Refund: broken eggs',
              createdAtUtc: '2026-10-15T08:30:00Z',
            },
            {
              id: 1,
              type: 'TopUp',
              amount: 500,
              balanceAfter: 500,
              description: 'Money added to wallet',
              createdAtUtc: '2026-10-14T08:30:00Z',
            },
          ],
          page: 1,
          pageSize: 20,
          totalCount: 2,
          totalPages: 1,
        },
      ],
    })
    renderApp('/account/wallet')

    expect(await screen.findByText('₹750.50')).toBeInTheDocument()
    const credit = (await screen.findByText('Credit from store')).closest('li')
    expect(within(credit).getByText('+₹250.50')).toBeInTheDocument()
    expect(within(credit).getByText('Refund: broken eggs')).toBeInTheDocument()
  })

  it('tops up with a quick amount and sends an idempotency key', async () => {
    const user = userEvent.setup()
    let balance = 0
    const calls = mockApi({
      ...signedInAs(customer),
      'GET /wallet': () => [200, { balance, ...limits }],
      'GET /wallet/transactions': noTransactions,
      'POST /wallet/top-ups': (body) => {
        balance += body.amount
        return [200, { id: 9, amount: body.amount, status: 'Succeeded', failureReason: null, balance }]
      },
    })
    renderApp('/account/wallet')

    await user.click(await screen.findByRole('button', { name: '₹500' }))
    await user.click(screen.getByRole('button', { name: 'Add ₹500.00' }))

    expect(await screen.findByText('₹500.00 added. New balance ₹500.00.')).toBeInTheDocument()
    const body = calls.find((c) => c.method === 'POST').body
    expect(body.amount).toBe(500)
    expect(body.idempotencyKey).toMatch(/^[0-9a-f-]{32,36}$/)
  })

  it('reuses the same key when retrying after a dropped connection, and a new key afterwards', async () => {
    const user = userEvent.setup()
    let attempts = 0
    const calls = mockApi({
      ...signedInAs(customer),
      'GET /wallet': () => [200, { balance: 0, ...limits }],
      'GET /wallet/transactions': noTransactions,
      'POST /wallet/top-ups': (body) => {
        attempts += 1
        if (attempts === 1) throw new TypeError('Failed to fetch') // the network drops
        return [200, { id: 3, amount: body.amount, status: 'Succeeded', failureReason: null, balance: body.amount }]
      },
    })
    renderApp('/account/wallet')

    await user.type(await screen.findByLabelText('Amount (₹)'), '200')
    await user.click(screen.getByRole('button', { name: 'Add ₹200.00' }))
    expect(await screen.findByText(/Could not reach the store/)).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Add ₹200.00' }))
    await screen.findByText(/₹200.00 added/)

    const keys = calls.filter((c) => c.method === 'POST').map((c) => c.body.idempotencyKey)
    expect(keys[0]).toBe(keys[1])
  })

  it('checks limits before contacting the server', async () => {
    const user = userEvent.setup()
    const calls = mockApi({
      ...signedInAs(customer),
      'GET /wallet': () => [200, { balance: 19950, ...limits }],
      'GET /wallet/transactions': noTransactions,
    })
    renderApp('/account/wallet')

    expect(await screen.findByRole('button', { name: '₹100' })).toBeDisabled() // only ₹50 of room left
    await user.type(screen.getByLabelText('Amount (₹)'), '60')
    await user.click(screen.getByRole('button', { name: 'Add ₹60.00' }))

    expect(await screen.findByText(/You can add ₹50.00/)).toBeInTheDocument()
    expect(calls.some((c) => c.method === 'POST')).toBe(false)
  })

  it('explains a declined payment', async () => {
    const user = userEvent.setup()
    mockApi({
      ...signedInAs(customer),
      'GET /wallet': () => [200, { balance: 0, ...limits }],
      'GET /wallet/transactions': noTransactions,
      'POST /wallet/top-ups': () => [
        200,
        { id: 4, amount: 100, status: 'Failed', failureReason: 'Card declined', balance: 0 },
      ],
    })
    renderApp('/account/wallet')

    await user.click(await screen.findByRole('button', { name: '₹100' }))
    await user.click(screen.getByRole('button', { name: 'Add ₹100.00' }))

    expect(await screen.findByText('Payment failed')).toBeInTheDocument()
    expect(screen.getByText(/Card declined/)).toBeInTheDocument()
  })
})

describe('admin wallet adjustment', () => {
  it('requires a note and sends the adjustment', async () => {
    const user = userEvent.setup()
    const admin = { ...customer, roles: ['Admin'] }
    const calls = mockApi({
      ...signedInAs(admin),
      'GET /admin/customers/7/wallet': () => [
        200,
        { userId: 7, fullName: 'Asha Verma', email: 'asha@example.com', phoneNumber: '9876543210', balance: 100 },
      ],
      'GET /admin/customers/7/wallet/transactions': noTransactions,
      'POST /admin/customers/7/wallet/adjustments': (body) => [
        200,
        {
          userId: 7,
          fullName: 'Asha Verma',
          email: 'asha@example.com',
          phoneNumber: '9876543210',
          balance: 100 + body.amount,
        },
      ],
    })
    window.confirm = () => true
    renderApp('/admin/customers/7')

    await user.type(await screen.findByLabelText('Amount (₹)'), '40')
    await user.click(screen.getByRole('button', { name: 'Add money' }))
    expect(await screen.findByText('Explain why, e.g. the order number or complaint.')).toBeInTheDocument()

    await user.type(screen.getByLabelText('Note'), 'Order 1042: eggs broken')
    await user.click(screen.getByRole('button', { name: 'Add money' }))

    await waitFor(() => expect(screen.getByText('Done. New balance ₹140.00.')).toBeInTheDocument())
    expect(calls.find((c) => c.method === 'POST').body).toEqual({
      direction: 'Credit',
      amount: 40,
      reason: 'Refund',
      note: 'Order 1042: eggs broken',
    })
  })
})
