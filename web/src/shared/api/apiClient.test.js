import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError, apiClient } from './apiClient'

function mockFetch(status, body, contentType = 'application/json') {
  globalThis.fetch = vi.fn().mockResolvedValue(
    new Response(body === undefined ? null : JSON.stringify(body), {
      status,
      headers: { 'content-type': contentType },
    }),
  )
}

afterEach(() => vi.restoreAllMocks())

describe('apiClient', () => {
  it('prefixes /api and returns parsed JSON', async () => {
    mockFetch(200, { status: 'Healthy' })

    await expect(apiClient.get('/health')).resolves.toEqual({ status: 'Healthy' })
    expect(fetch).toHaveBeenCalledWith('/api/health', expect.objectContaining({ method: 'GET' }))
  })

  it('sends JSON bodies', async () => {
    mockFetch(201, { id: 1 })

    await apiClient.post('/things', { name: 'Rice' })

    const [, init] = fetch.mock.calls[0]
    expect(init.body).toBe('{"name":"Rice"}')
    expect(init.headers['Content-Type']).toBe('application/json')
  })

  it('turns ProblemDetails responses into ApiError', async () => {
    mockFetch(
      422,
      { title: 'Business rule violated', detail: 'Only 2 left in stock.', traceId: 'abc' },
      'application/problem+json',
    )

    const error = await apiClient.get('/x').catch((e) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect(error.status).toBe(422)
    expect(error.message).toBe('Only 2 left in stock.')
    expect(error.traceId).toBe('abc')
  })

  it('exposes field validation errors', async () => {
    mockFetch(
      400,
      { title: 'Validation failed', errors: { Email: ['Email is required.'] } },
      'application/problem+json',
    )

    const error = await apiClient.post('/x', {}).catch((e) => e)

    expect(error.errors.Email).toEqual(['Email is required.'])
  })

  it('reports network failures as status 0', async () => {
    globalThis.fetch = vi.fn().mockRejectedValue(new TypeError('Failed to fetch'))

    const error = await apiClient.get('/x').catch((e) => e)

    expect(error.isNetworkError).toBe(true)
  })
})
