import { ApiError } from './apiClient'

/** True when the API rejected specific fields (400 with an `errors` map) rather than the whole request. */
export function hasFieldErrors(error) {
  return error instanceof ApiError && error.status === 400 && Object.keys(error.errors).some(Boolean)
}

/**
 * Shows server-side validation messages under the matching react-hook-form fields.
 * The API uses the same camelCase names as our form fields, so no mapping table is needed.
 */
export function applyServerErrors(error, setError) {
  if (!hasFieldErrors(error)) return false
  for (const [field, messages] of Object.entries(error.errors)) {
    if (field) setError(field, { type: 'server', message: messages[0] })
  }
  return true
}

/** The error to show in a form-level alert: anything that isn't already shown next to a field. */
export function formLevelError(error) {
  return error && !hasFieldErrors(error) ? error : null
}
