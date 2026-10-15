import { render } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { RouterProvider, createMemoryRouter } from 'react-router'
import { vi } from 'vitest'
import { routes } from '../app/router'

/**
 * Fake API for component tests. `handlers` maps "METHOD /path" to a function returning
 * [status, body]. Every request is recorded so tests can assert on what was sent.
 * Unmatched requests fail loudly instead of silently returning nothing.
 */
export function mockApi(handlers) {
  const calls = []
  globalThis.fetch = vi.fn(async (url, init = {}) => {
    const method = init.method ?? 'GET'
    const path = String(url)
      .replace(/^\/api/, '')
      .split('?')[0]
    const body = init.body ? JSON.parse(init.body) : undefined
    calls.push({ method, path, body })

    const handler = handlers[`${method} ${path}`]
    if (!handler) throw new Error(`No mock for ${method} ${path}`)
    const [status, responseBody] = handler(body)
    const isProblem = status >= 400
    return new Response(responseBody === undefined ? null : JSON.stringify(responseBody), {
      status,
      headers: { 'content-type': isProblem ? 'application/problem+json' : 'application/json' },
    })
  })
  return calls
}

export const anonymous = { 'GET /auth/me': () => [401, { title: 'Unauthorized', status: 401 }] }

export const customer = {
  id: 7,
  fullName: 'Asha Verma',
  email: 'asha@example.com',
  phoneNumber: '9876543210',
  roles: ['Customer'],
}

export const signedInAs = (user) => ({ 'GET /auth/me': () => [200, user] })

/** Renders the real route table at `path`, with a fresh query cache per test. */
export function renderApp(path) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const router = createMemoryRouter(routes, { initialEntries: [path] })
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
  return { router }
}
