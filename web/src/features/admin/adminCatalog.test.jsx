import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { mockApi, renderApp, signedInAs } from '../../test/renderApp'

const admin = { id: 1, fullName: 'Store Admin', email: 'admin@x.test', phoneNumber: null, roles: ['Admin'] }

const product = {
  id: 3,
  categoryId: 1,
  categoryName: 'Dairy',
  name: 'Fresh Paneer',
  brand: 'Amul',
  description: null,
  unitLabel: '200 g',
  sku: null,
  price: 90,
  mrp: 95,
  discountPercent: 5,
  stockQuantity: 12,
  lowStockThreshold: 5,
  maxPerOrder: 10,
  stockStatus: 'InStock',
  imageUrl: null,
  isActive: true,
  isFeatured: false,
  version: 4,
  updatedAtUtc: '2026-10-15T08:00:00Z',
}

const baseHandlers = {
  ...signedInAs(admin),
  'GET /admin/categories': () => [
    200,
    [
      {
        id: 1,
        name: 'Dairy',
        slug: 'dairy',
        icon: '🥛',
        description: null,
        sortOrder: 0,
        isActive: true,
        productCount: 1,
      },
    ],
  ],
  'GET /admin/products/3': () => [200, product],
  'GET /admin/products/3/stock-adjustments': () => [200, []],
}

describe('admin product editor', () => {
  it('sends the version it loaded and explains a conflict instead of overwriting', async () => {
    const user = userEvent.setup()
    const calls = mockApi({
      ...baseHandlers,
      'PUT /admin/products/3': () => [409, { status: 409, title: 'Conflict', detail: 'The record was changed.' }],
    })
    renderApp('/admin/products/3')

    const price = await screen.findByLabelText('Selling price (₹)')
    await user.clear(price)
    await user.type(price, '85')
    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    expect(await screen.findByText('Someone else changed this product')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Reload product' })).toBeInTheDocument()
    expect(calls.find((c) => c.method === 'PUT').body).toMatchObject({ price: 85, version: 4 })
  })

  it('checks MRP against price before saving', async () => {
    const user = userEvent.setup()
    const calls = mockApi(baseHandlers)
    renderApp('/admin/products/3')

    const mrp = await screen.findByLabelText('MRP (₹)')
    await user.clear(mrp)
    await user.type(mrp, '80')
    await user.click(screen.getByRole('button', { name: 'Save changes' }))

    expect(await screen.findByText("MRP can't be lower than the selling price.")).toBeInTheDocument()
    expect(calls.some((c) => c.method === 'PUT')).toBe(false)
  })

  it('turns "Damaged 3" into a stock change of −3', async () => {
    const user = userEvent.setup()
    // A stateful fake: after the POST, re-fetching the product returns the new stock, like the real API.
    let current = product
    const calls = mockApi({
      ...baseHandlers,
      'GET /admin/products/3': () => [200, current],
      'POST /admin/products/3/stock-adjustments': (body) => {
        current = { ...current, stockQuantity: current.stockQuantity + body.quantityChange }
        return [200, current]
      },
    })
    renderApp('/admin/products/3')

    await user.selectOptions(await screen.findByLabelText('Reason'), 'Damaged')
    await user.type(screen.getByLabelText('Quantity'), '3')
    await user.click(screen.getByRole('button', { name: 'Update stock' }))

    expect(
      await screen.findByText((_, el) => el?.tagName === 'P' && el.textContent === '9 in stock'),
    ).toBeInTheDocument()
    expect(calls.find((c) => c.path.endsWith('stock-adjustments') && c.method === 'POST').body).toEqual({
      quantityChange: -3,
      reason: 'Damaged',
      note: '',
    })
  })
})
