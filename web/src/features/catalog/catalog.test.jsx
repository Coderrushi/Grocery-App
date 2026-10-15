import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { anonymous, mockApi, renderApp } from '../../test/renderApp'
import { productUrl } from './productUrl'

const categories = [
  { id: 1, name: 'Dairy, Bread & Eggs', slug: 'dairy-bread-eggs', icon: '🥛', description: null, productCount: 2 },
  { id: 2, name: 'Beverages', slug: 'beverages', icon: '🧃', description: null, productCount: 1 },
]

const card = (overrides) => ({
  id: 1,
  name: 'Taaza Toned Milk',
  brand: 'Amul',
  unitLabel: '500 ml',
  price: 28,
  mrp: 28,
  discountPercent: 0,
  imageUrl: null,
  categorySlug: 'dairy-bread-eggs',
  categoryIcon: '🥛',
  stockStatus: 'InStock',
  onlyLeft: null,
  maxQuantity: 10,
  ...overrides,
})

const page = (items, extra = {}) => ({
  items,
  page: 1,
  pageSize: 24,
  totalCount: items.length,
  totalPages: 1,
  ...extra,
})

describe('storefront catalogue', () => {
  it('home page shows category tiles and featured products', async () => {
    const calls = mockApi({
      ...anonymous,
      'GET /catalog/categories': () => [200, categories],
      'GET /catalog/products': () => [
        200,
        page([card({ id: 5, name: 'Paneer', price: 90, mrp: 95, discountPercent: 5 })]),
      ],
    })
    renderApp('/')

    expect(await screen.findByRole('link', { name: /beverages/i })).toHaveAttribute('href', '/category/beverages')
    const paneer = (await screen.findByRole('link', { name: 'Paneer' })).closest('li')
    expect(within(paneer).getByText('₹90.00')).toBeInTheDocument()
    expect(within(paneer).getByText('5% off')).toBeInTheDocument()
    expect(calls.find((c) => c.path === '/catalog/products')).toBeTruthy()
    expect(fetch.mock.calls.some(([url]) => String(url).includes('featured=true'))).toBe(true)
  })

  it('category page keeps sort and stock filter in the URL and sends them to the API', async () => {
    const user = userEvent.setup()
    mockApi({
      ...anonymous,
      'GET /catalog/categories': () => [200, categories],
      'GET /catalog/products': () => [
        200,
        page([card({ id: 9, name: 'Masti Dahi', stockStatus: 'OutOfStock', maxQuantity: 0 })]),
      ],
    })
    const { router } = renderApp('/category/dairy-bread-eggs')

    expect(await screen.findByRole('heading', { name: /dairy, bread & eggs/i })).toBeInTheDocument()
    expect(await screen.findByText('Out of stock')).toBeInTheDocument()

    await user.selectOptions(screen.getByLabelText('Sort by'), 'PriceLowToHigh')
    await user.click(screen.getByLabelText('In stock only'))

    await waitFor(() => expect(router.state.location.search).toBe('?sort=PriceLowToHigh&inStock=1'))
    await waitFor(() =>
      expect(
        fetch.mock.calls.some(
          ([url]) => String(url).includes('sort=PriceLowToHigh') && String(url).includes('inStockOnly=true'),
        ),
      ).toBe(true),
    )
  })

  it('"Show more" loads the next page', async () => {
    const user = userEvent.setup()
    mockApi({
      ...anonymous,
      'GET /catalog/categories': () => [200, categories],
      'GET /catalog/products': () => {
        const url = String(fetch.mock.calls.at(-1)[0])
        return url.includes('page=2')
          ? [200, page([card({ id: 2, name: 'Second page item' })], { page: 2, totalCount: 2, totalPages: 2 })]
          : [200, page([card({ id: 1, name: 'First page item' })], { totalCount: 2, totalPages: 2 })]
      },
    })
    renderApp('/search?q=item')

    expect(await screen.findByText('First page item')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Show more' }))

    expect(await screen.findByText('Second page item')).toBeInTheDocument()
    expect(screen.getByText('First page item')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Show more' })).not.toBeInTheDocument()
  })

  it('header search goes to the results page', async () => {
    const user = userEvent.setup()
    mockApi({
      ...anonymous,
      'GET /catalog/categories': () => [200, categories],
      'GET /catalog/products': () => [200, page([])],
    })
    const { router } = renderApp('/')

    const [searchBox] = await screen.findAllByRole('searchbox', { name: 'Search products' })
    await user.type(searchBox, 'basmati rice{Enter}')

    await waitFor(() => expect(router.state.location.pathname).toBe('/search'))
    expect(router.state.location.search).toBe('?q=basmati%20rice')
    expect(await screen.findByText(/couldn't find anything/i)).toBeInTheDocument()
  })

  it('product page shows price, MRP, saving and low stock', async () => {
    mockApi({
      ...anonymous,
      'GET /catalog/products/5': () => [
        200,
        {
          product: card({
            id: 5,
            name: 'Fresh Paneer',
            price: 90,
            mrp: 95,
            discountPercent: 5,
            stockStatus: 'LowStock',
            onlyLeft: 3,
          }),
          description: 'Soft and fresh.',
          categoryName: 'Dairy, Bread & Eggs',
        },
      ],
    })
    renderApp('/products/5/fresh-paneer')

    expect(await screen.findByRole('heading', { name: 'Fresh Paneer' })).toBeInTheDocument()
    expect(screen.getByText('₹95.00')).toBeInTheDocument()
    expect(screen.getByText('Only 3 left')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Dairy, Bread & Eggs' })).toHaveAttribute(
      'href',
      '/category/dairy-bread-eggs',
    )
  })

  it('unknown or hidden products show "not found"', async () => {
    mockApi({ ...anonymous, 'GET /catalog/products/999': () => [404, { status: 404, title: 'Not found' }] })
    renderApp('/products/999')

    expect(await screen.findByText('Page not found')).toBeInTheDocument()
  })
})

describe('productUrl', () => {
  it.each([
    [{ id: 42, name: 'Amul Taaza Toned Milk (500 ml)' }, '/products/42/amul-taaza-toned-milk-500-ml'],
    [{ id: 7, name: 'Café Crème' }, '/products/7/cafe-creme'],
    [{ id: 8, name: 'दूध' }, '/products/8'],
  ])('%o -> %s', (product, expected) => expect(productUrl(product)).toBe(expected))
})
