import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { customer, mockApi, renderApp, signedInAs } from '../../test/renderApp'
import { addDays, istToday, matchPreset, presetPeriod } from './period'
import { niceTicks } from './chartScale'

const admin = { ...customer, roles: ['Admin'] }

const day = (date, sales = 0, orders = 0, cancelledOrders = 0) => ({ date, sales, orders, cancelledOrders })

const orderSummary = (id, status = 'Placed') => ({
  id,
  orderNumber: `QK-${String(id).padStart(6, '0')}`,
  status,
  placedAtUtc: '2026-10-16T04:30:00Z',
  total: 225,
  itemCount: 2,
  customerName: 'Asha Verma',
  customerPhone: '9876543210',
  pincode: '411038',
})

const dashboard = {
  today: '2026-10-16',
  todaySales: { orders: 3, sales: 1250, averageOrderValue: 416.67 },
  yesterdaySales: { orders: 5, sales: 2000, averageOrderValue: 400 },
  cancelledToday: 1,
  openOrders: [
    { status: 'Placed', count: 4 },
    { status: 'Confirmed', count: 2 },
    { status: 'Packed', count: 0 },
    { status: 'OutForDelivery', count: 1 },
  ],
  last7Days: ['10', '11', '12', '13', '14', '15', '16'].map((d, i) => day(`2026-10-${d}`, i * 100, i)),
  outOfStockCount: 1,
  lowStockCount: 2,
  stockAlerts: [
    { productId: 9, name: 'Toor Dal', brand: null, unitLabel: '1 kg', stockQuantity: 0, lowStockThreshold: 5 },
  ],
  recentOrders: [orderSummary(42)],
}

const fetchedUrls = () => globalThis.fetch.mock.calls.map(([url]) => String(url))

describe('admin dashboard', () => {
  it('shows the order queue, today against yesterday, stock alerts and the week', async () => {
    mockApi({ ...signedInAs(admin), 'GET /admin/dashboard': () => [200, dashboard] })

    renderApp('/admin')

    expect(await screen.findByRole('heading', { name: 'Dashboard' })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Placed\s*4/ })).toHaveAttribute('href', '/admin/orders')
    expect(screen.getByRole('link', { name: /Out for delivery\s*1/ })).toHaveAttribute(
      'href',
      '/admin/orders?status=OutForDelivery',
    )
    expect(screen.getByText('₹1,250.00')).toBeInTheDocument()
    expect(screen.getByText('Down from ₹2,000.00 yesterday')).toBeInTheDocument()
    expect(screen.getByText('1 cancelled today (not counted)')).toBeInTheDocument()
    expect(screen.getByText('Out of stock')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'QK-000042' })).toHaveAttribute('href', '/admin/orders/42')
    // Every chart value is reachable without hovering: each day is a labelled, focusable target.
    expect(screen.getByLabelText('Fri, 16 Oct, 2026: ₹600.00 from 6 orders')).toBeInTheDocument()
  })

  it('shows a tooltip for the day under the pointer or keyboard focus', async () => {
    mockApi({ ...signedInAs(admin), 'GET /admin/dashboard': () => [200, dashboard] })
    renderApp('/admin')

    const target = await screen.findByLabelText(/Thu, 15 Oct, 2026/)
    target.focus()

    const tooltip = await screen.findByRole('tooltip')
    expect(tooltip).toHaveTextContent('₹500.00')
    expect(tooltip).toHaveTextContent('5 orders')
  })
})

const salesReport = (from, to) => ({
  from,
  to,
  totals: {
    orders: 2,
    sales: 300,
    itemSales: 250,
    deliveryFees: 50,
    customerSavings: 30,
    averageOrderValue: 150,
    deliveredSales: 225,
    inProgressSales: 75,
    cancelledOrders: 1,
    cancelledValue: 125,
  },
  days: [day(from, 300, 2, 1)],
  topByRevenue: [
    { productId: 1, name: 'Basmati Rice', brand: null, unitLabel: '1 kg', quantity: 2, sales: 200 },
    { productId: 2, name: 'Salt', brand: null, unitLabel: '1 kg', quantity: 5, sales: 50 },
  ],
  topByQuantity: [
    { productId: 2, name: 'Salt', brand: null, unitLabel: '1 kg', quantity: 5, sales: 50 },
    { productId: 1, name: 'Basmati Rice', brand: null, unitLabel: '1 kg', quantity: 2, sales: 200 },
  ],
  categories: [{ categoryId: 3, name: 'Staples', quantity: 7, sales: 250 }],
})

describe('sales report', () => {
  it('defaults to the last 30 days, changes period from the picker and links CSVs for the same dates', async () => {
    const user = userEvent.setup()
    const today = istToday()
    mockApi({
      ...signedInAs(admin),
      'GET /admin/reports/sales': () => [200, salesReport(addDays(today, -29), today)],
    })

    renderApp('/admin/reports')

    expect(await screen.findByText('₹225.00 delivered · ₹75.00 in progress')).toBeInTheDocument()
    expect(fetchedUrls()).toContain(`/api/admin/reports/sales?from=${addDays(today, -29)}&to=${today}`)
    expect(screen.getByText('₹125.00 refunded to wallets')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /All orders/ })).toHaveAttribute(
      'href',
      `/api/admin/reports/orders.csv?from=${addDays(today, -29)}&to=${today}`,
    )

    await user.selectOptions(screen.getByLabelText('Period'), 'Last 7 days')

    await waitFor(() =>
      expect(fetchedUrls()).toContain(`/api/admin/reports/sales?from=${addDays(today, -6)}&to=${today}`),
    )
  })

  it('ranks top products by sales or by units', async () => {
    const user = userEvent.setup()
    mockApi({ ...signedInAs(admin), 'GET /admin/reports/sales': () => [200, salesReport('2026-10-01', '2026-10-30')] })
    renderApp('/admin/reports')

    const topCard = (await screen.findByRole('heading', { name: 'Top products' })).closest('section')
    expect(within(topCard).getAllByRole('listitem')[0]).toHaveTextContent('Basmati Rice')

    await user.click(within(topCard).getByRole('button', { name: 'Units' }))

    expect(within(topCard).getAllByRole('listitem')[0]).toHaveTextContent('Salt')
    expect(within(topCard).getAllByRole('listitem')[0]).toHaveTextContent('5 sold')
  })

  it('checks custom dates before asking the server', async () => {
    const user = userEvent.setup()
    mockApi({ ...signedInAs(admin), 'GET /admin/reports/sales': () => [200, salesReport('2026-10-01', '2026-10-30')] })
    renderApp('/admin/reports?from=2026-10-01&to=2026-10-30')

    await screen.findByText('₹225.00 delivered · ₹75.00 in progress')
    await user.selectOptions(screen.getByLabelText('Period'), 'Custom dates…')
    await user.clear(screen.getByLabelText('To'))
    await user.type(screen.getByLabelText('To'), '2026-09-01')

    expect(screen.getByText('The end date must be on or after the start date.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Show' })).toBeDisabled()
  })
})

describe('stock and customer reports', () => {
  it('lists what to reorder with days of stock left', async () => {
    mockApi({
      ...signedInAs(admin),
      'GET /admin/reports/inventory': () => [
        200,
        {
          from: '2026-10-01',
          to: '2026-10-30',
          activeProducts: 40,
          lowStockCount: 1,
          outOfStockCount: 1,
          stockValueAtPrice: 12345,
          reorder: [
            {
              productId: 9,
              name: 'Toor Dal',
              brand: null,
              unitLabel: '1 kg',
              category: 'Staples',
              stockQuantity: 3,
              lowStockThreshold: 5,
              soldLast30Days: 6,
              daysOfCover: 15,
            },
          ],
          movements: [{ reason: 'Damaged', entries: 1, units: -2 }],
          losses: [{ productId: 9, name: 'Toor Dal', unitLabel: '1 kg', damaged: 2, expired: 0, value: 280 }],
          lossValue: 280,
        },
      ],
    })

    renderApp('/admin/reports/inventory?from=2026-10-01&to=2026-10-30')

    const row = (await screen.findByRole('link', { name: /Toor Dal/ })).closest('tr')
    expect(row).toHaveTextContent('3 / 5')
    expect(row).toHaveTextContent('15')
    expect(screen.getByText('Damaged or expired: ₹280.00 at selling price')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Reorder list/ })).toHaveAttribute(
      'href',
      '/api/admin/reports/inventory/reorder.csv',
    )
  })

  it('shows buyers, top customers and the money held in wallets', async () => {
    mockApi({
      ...signedInAs(admin),
      'GET /admin/reports/customers': () => [
        200,
        {
          from: '2026-10-01',
          to: '2026-10-30',
          newCustomers: 4,
          orderingCustomers: 10,
          firstTimeCustomers: 3,
          returningCustomers: 7,
          topCustomers: [
            {
              userId: 7,
              fullName: 'Asha Verma',
              phoneNumber: '9876543210',
              orders: 4,
              spent: 1800,
              lastOrderAtUtc: '2026-10-15T08:30:00Z',
            },
          ],
          walletBalanceTotal: 5400,
          customersWithBalance: 6,
          walletFlows: [{ type: 'TopUp', count: 3, amount: 2500 }],
        },
      ],
    })

    renderApp('/admin/reports/customers?from=2026-10-01&to=2026-10-30')

    expect(await screen.findByText('₹5,400.00')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Asha Verma\s*9876543210/ })).toHaveAttribute('href', '/admin/customers/7')
    expect(screen.getByText('Money added').closest('tr')).toHaveTextContent('+₹2,500.00')
  })
})

describe('bulk order actions', () => {
  it('confirms the selected new orders together and reports the ones that could not move', async () => {
    const user = userEvent.setup()
    const calls = mockApi({
      ...signedInAs(admin),
      'GET /admin/orders': () => [
        200,
        { items: [orderSummary(41), orderSummary(42)], page: 1, pageSize: 20, totalCount: 2, totalPages: 1 },
      ],
      'POST /admin/orders/bulk-status': () => [
        200,
        {
          updated: [41],
          failed: [
            {
              orderId: 42,
              orderNumber: 'QK-000042',
              reason: "An order that is Cancelled can't be moved to Confirmed.",
            },
          ],
        },
      ],
    })

    renderApp('/admin/orders')

    await user.click(await screen.findByLabelText('Select all orders on this page'))
    await user.click(screen.getByRole('button', { name: /Confirm order \(2\)/ }))

    expect(await screen.findByText('1 order updated')).toBeInTheDocument()
    expect(screen.getByText(/QK-000042: An order that is Cancelled/)).toBeInTheDocument()
    expect(calls.find((c) => c.method === 'POST').body).toEqual({ orderIds: [41, 42], status: 'Confirmed' })
  })

  it('offers no bulk selection where orders cannot move forward together', async () => {
    mockApi({
      ...signedInAs(admin),
      'GET /admin/orders': () => [
        200,
        { items: [orderSummary(41, 'Delivered')], page: 1, pageSize: 20, totalCount: 1, totalPages: 1 },
      ],
    })

    renderApp('/admin/orders?status=Delivered')

    expect(await screen.findByRole('link', { name: 'QK-000041' })).toBeInTheDocument()
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
  })
})

describe('packing slip', () => {
  it('prints the items, address and bill without the store navigation', async () => {
    mockApi({
      ...signedInAs(admin),
      'GET /admin/orders/42': () => [
        200,
        {
          order: {
            id: 42,
            orderNumber: 'QK-000042',
            status: 'Confirmed',
            paymentMethod: 'Wallet',
            paymentStatus: 'Paid',
            placedAtUtc: '2026-10-16T04:30:00Z',
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
            deliveryAddress: {
              recipientName: 'Asha Verma',
              phoneNumber: '9876543210',
              line1: 'Flat 4B',
              line2: null,
              landmark: 'Big temple',
              city: 'Pune',
              state: 'Maharashtra',
              pincode: '411038',
            },
            deliveryInstructions: 'Ring twice',
            cancellationReason: null,
            timeline: [],
            canCancel: false,
          },
          customer: { id: 7, fullName: 'Asha Verma', email: 'asha@example.com', phoneNumber: '9876543210' },
          allowedNextStatuses: ['Packed', 'Cancelled'],
          version: 1,
        },
      ],
    })

    renderApp('/admin/orders/42/print')

    expect(await screen.findByText('Packing slip & bill')).toBeInTheDocument()
    expect(screen.getByText('Fresh Paneer').closest('tr')).toHaveTextContent('₹200.00')
    expect(screen.getByText('Near Big temple')).toBeInTheDocument()
    expect(screen.getByText('Ring twice')).toBeInTheDocument()
    expect(screen.getByText('From wallet, nothing to collect')).toBeInTheDocument()
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument()
  })
})

describe('report periods', () => {
  it('builds presets from the Indian date', () => {
    expect(presetPeriod('7d', '2026-03-05')).toEqual({ from: '2026-02-27', to: '2026-03-05' })
    expect(presetPeriod('month', '2026-03-05')).toEqual({ from: '2026-03-01', to: '2026-03-05' })
    expect(presetPeriod('last-month', '2026-03-05')).toEqual({ from: '2026-02-01', to: '2026-02-28' })
    expect(matchPreset({ from: '2026-02-04', to: '2026-03-05' }, '2026-03-05')).toBe('30d')
    expect(matchPreset({ from: '2026-01-01', to: '2026-01-09' }, '2026-03-05')).toBeUndefined()
  })

  it('uses the date in India, not the browser time zone', () => {
    // 20:00 UTC on the 15th is 01:30 IST on the 16th.
    expect(istToday(new Date('2026-10-15T20:00:00Z'))).toBe('2026-10-16')
  })

  it('rounds chart axes to clean steps', () => {
    expect(niceTicks(1250)).toEqual([0, 500, 1000, 1500])
    expect(niceTicks(0)).toEqual([0, 100])
  })
})
