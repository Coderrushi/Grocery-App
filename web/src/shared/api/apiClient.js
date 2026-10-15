/**
 * The single gateway between the React app and the ASP.NET Core API.
 * Components never call fetch() directly: they use feature-level functions built on apiClient,
 * usually wrapped in TanStack Query hooks.
 */

const BASE_URL = '/api'

/** An error returned by the API as RFC 7807 ProblemDetails, or a network failure. */
export class ApiError extends Error {
  constructor({ status, title, detail, errors, traceId }) {
    super(detail || title || 'Request failed')
    this.name = 'ApiError'
    this.status = status
    this.title = title
    this.detail = detail
    /** Field validation errors, e.g. { Email: ['Email is required.'] } */
    this.errors = errors ?? {}
    this.traceId = traceId
  }

  get isNetworkError() {
    return this.status === 0
  }
}

async function request(path, { method = 'GET', body, signal, headers } = {}) {
  // FormData (file uploads) is sent as-is so the browser sets the multipart boundary; anything else is JSON.
  const isForm = body instanceof FormData
  let response
  try {
    response = await fetch(`${BASE_URL}${path}`, {
      method,
      signal,
      credentials: 'same-origin', // sends the auth cookie
      headers: {
        Accept: 'application/json',
        // Proves the request came from our own scripts; required by upload endpoints (CSRF defence).
        'X-Requested-With': 'XMLHttpRequest',
        ...(body !== undefined && !isForm && { 'Content-Type': 'application/json' }),
        ...headers,
      },
      body: body === undefined || isForm ? body : JSON.stringify(body),
    })
  } catch (error) {
    if (error.name === 'AbortError') throw error
    throw new ApiError({
      status: 0,
      title: 'Network error',
      detail: 'Could not reach the store. Check your internet connection and try again.',
    })
  }

  if (response.status === 204) return null

  const isJson = response.headers.get('content-type')?.includes('json')
  const payload = isJson ? await response.json() : null

  if (!response.ok) {
    throw new ApiError({
      status: response.status,
      title: payload?.title ?? response.statusText,
      detail: payload?.detail,
      errors: payload?.errors,
      traceId: payload?.traceId,
    })
  }

  return payload
}

export const apiClient = {
  get: (path, options) => request(path, { ...options, method: 'GET' }),
  post: (path, body, options) => request(path, { ...options, method: 'POST', body }),
  put: (path, body, options) => request(path, { ...options, method: 'PUT', body }),
  patch: (path, body, options) => request(path, { ...options, method: 'PATCH', body }),
  delete: (path, options) => request(path, { ...options, method: 'DELETE' }),
}
