import { act, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it } from 'vitest'
import { anonymous, mockApi, renderApp } from '../../test/renderApp'
import { resolveTheme, setTheme } from './theme'

const html = document.documentElement

afterEach(() => act(() => setTheme('system')))

describe('theme', () => {
  it('applies and remembers an explicit choice', () => {
    act(() => setTheme('dark'))

    expect(html.dataset.theme).toBe('dark')
    expect(html.style.colorScheme).toBe('dark')
    expect(localStorage.getItem('qk_theme')).toBe('dark')

    act(() => setTheme('light'))
    expect(html.dataset.theme).toBe('light')
  })

  it('"System" follows the device and stores nothing', () => {
    act(() => setTheme('dark'))
    act(() => setTheme('system'))

    expect(localStorage.getItem('qk_theme')).toBeNull()
    // jsdom has no dark-mode media query, so the device counts as light.
    expect(resolveTheme('system')).toBe('light')
    expect(html.dataset.theme).toBe('light')
  })

  it('ignores unknown values', () => {
    act(() => setTheme('dark'))
    act(() => setTheme('purple'))
    expect(html.dataset.theme).toBe('dark')
  })

  it('header menu offers Light, Dark and System and marks the current one', async () => {
    const user = userEvent.setup()
    mockApi({
      ...anonymous,
      'GET /catalog/categories': () => [200, []],
      'GET /catalog/products': () => [200, { items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 }],
    })
    renderApp('/')

    await user.click(screen.getByRole('button', { name: 'Theme: System' }))
    expect(screen.getByRole('menuitemradio', { name: /System/ })).toHaveAttribute('aria-checked', 'true')

    await user.click(screen.getByRole('menuitemradio', { name: 'Dark' }))

    expect(html.dataset.theme).toBe('dark')
    expect(screen.queryByRole('menu')).not.toBeInTheDocument() // closes after choosing
    expect(screen.getByRole('button', { name: 'Theme: Dark' })).toHaveFocus()
  })

  it('menu closes with Escape', async () => {
    const user = userEvent.setup()
    mockApi({
      ...anonymous,
      'GET /catalog/categories': () => [200, []],
      'GET /catalog/products': () => [200, { items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 }],
    })
    renderApp('/')

    await user.click(screen.getByRole('button', { name: /Theme:/ }))
    await user.keyboard('{Escape}')

    expect(screen.queryByRole('menu')).not.toBeInTheDocument()
  })
})
