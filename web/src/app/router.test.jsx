import { render, screen } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider, createMemoryRouter } from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import { routes } from './router'

function renderAt(path) {
  globalThis.fetch = vi.fn().mockResolvedValue(Response.json({ status: 'Healthy' }))
  const router = createMemoryRouter(routes, { initialEntries: [path] })
  render(
    <QueryClientProvider client={new QueryClient()}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
}

describe('routes', () => {
  it('renders the home page inside the store layout', async () => {
    renderAt('/')
    expect(await screen.findByRole('heading', { level: 1 })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /skip to content/i })).toBeInTheDocument()
  })

  it('shows a friendly page for unknown URLs', async () => {
    renderAt('/no-such-page')
    expect(await screen.findByText('Page not found')).toBeInTheDocument()
  })
})
