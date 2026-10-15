import { useId } from 'react'

// Text inputs are 16px so iOS Safari doesn't zoom in on focus, and 44px tall for easy tapping.
const controlClass =
  'block w-full rounded-lg border bg-surface px-3 text-base text-ink placeholder:text-ink-muted/70 ' +
  'focus:border-brand-600 focus:ring-2 focus:ring-brand-100 focus:outline-none disabled:bg-canvas'
const borderClass = (error) => (error ? 'border-danger-600' : 'border-line')

/** Label + control + hint/error wiring, with the ARIA attributes screen readers need. */
function useFieldIds(id) {
  const autoId = useId()
  const inputId = id ?? autoId
  return { inputId, hintId: `${inputId}-hint`, errorId: `${inputId}-error` }
}

function FieldMessages({ hint, error, hintId, errorId }) {
  if (error) {
    return (
      <p id={errorId} className="text-danger-700 mt-1 text-sm">
        {error.message ?? error}
      </p>
    )
  }
  return hint ? (
    <p id={hintId} className="text-ink-muted mt-1 text-sm">
      {hint}
    </p>
  ) : null
}

function describedBy(hint, error, hintId, errorId) {
  if (error) return errorId
  return hint ? hintId : undefined
}

export function TextField({ label, error, hint, id, className = '', ...inputProps }) {
  const { inputId, hintId, errorId } = useFieldIds(id)
  return (
    <div className={className}>
      <label htmlFor={inputId} className="mb-1 block text-sm font-medium">
        {label}
      </label>
      <input
        id={inputId}
        aria-invalid={error ? 'true' : undefined}
        aria-describedby={describedBy(hint, error, hintId, errorId)}
        className={`${controlClass} ${borderClass(error)} h-11`}
        {...inputProps}
      />
      <FieldMessages hint={hint} error={error} hintId={hintId} errorId={errorId} />
    </div>
  )
}

export function SelectField({ label, error, hint, id, className = '', children, ...selectProps }) {
  const { inputId, hintId, errorId } = useFieldIds(id)
  return (
    <div className={className}>
      <label htmlFor={inputId} className="mb-1 block text-sm font-medium">
        {label}
      </label>
      <select
        id={inputId}
        aria-invalid={error ? 'true' : undefined}
        aria-describedby={describedBy(hint, error, hintId, errorId)}
        className={`${controlClass} ${borderClass(error)} h-11`}
        {...selectProps}
      >
        {children}
      </select>
      <FieldMessages hint={hint} error={error} hintId={hintId} errorId={errorId} />
    </div>
  )
}

export function CheckboxField({ label, id, className = '', ...inputProps }) {
  const autoId = useId()
  const inputId = id ?? autoId
  return (
    <div className={`flex items-center gap-3 ${className}`}>
      <input id={inputId} type="checkbox" className="border-line accent-brand-600 size-5 rounded" {...inputProps} />
      <label htmlFor={inputId} className="text-sm">
        {label}
      </label>
    </div>
  )
}
