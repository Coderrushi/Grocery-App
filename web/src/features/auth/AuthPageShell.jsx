import { Card } from '../../shared/ui/Card'

export function AuthPageShell({ title, description, children, footer }) {
  return (
    <div className="mx-auto max-w-md py-4 sm:py-10">
      <Card>
        <h1 className="text-2xl font-bold">{title}</h1>
        {description && <p className="text-ink-muted mt-1">{description}</p>}
        <div className="mt-6">{children}</div>
      </Card>
      {footer && <p className="text-ink-muted mt-4 text-center text-sm">{footer}</p>}
    </div>
  )
}
