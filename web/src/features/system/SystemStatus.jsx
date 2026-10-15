import { useApiHealth } from './useApiHealth'

/** Development aid: shows whether the browser -> Vite proxy -> API -> MySQL chain is working. */
export function SystemStatus() {
  const { data, error, isPending } = useApiHealth()

  const healthy = data?.status === 'Healthy'
  const label = isPending ? 'Checking…' : healthy ? 'API and database connected' : 'API or database unavailable'
  const dot = isPending ? 'bg-ink-muted' : healthy ? 'bg-success-600' : 'bg-danger-600'

  return (
    <div className="border-line bg-surface inline-flex items-center gap-2 rounded-full border px-3 py-1.5 text-sm">
      <span className={`size-2.5 rounded-full ${dot}`} aria-hidden="true" />
      <span>{label}</span>
      {error && <span className="text-ink-muted">({error.status || 'network'})</span>}
    </div>
  )
}
