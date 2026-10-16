import { STATUS } from './orderStatusInfo'

const JOURNEY = ['Placed', 'Confirmed', 'Packed', 'OutForDelivery', 'Delivered']

export function OrderStatusBadge({ status }) {
  const s = STATUS[status] ?? { short: status, tone: 'bg-canvas text-ink' }
  return <span className={`inline-block rounded-full px-2.5 py-0.5 text-xs font-semibold ${s.tone}`}>{s.short}</span>
}

/** The delivery journey as a stepper: done steps are filled, the current one is highlighted. */
export function OrderProgress({ status }) {
  if (status === 'Cancelled') return null
  const current = JOURNEY.indexOf(status)
  return (
    <ol aria-label="Order progress" className="flex items-start">
      {JOURNEY.map((step, index) => {
        const done = index <= current
        return (
          <li
            key={step}
            className="flex flex-1 flex-col items-center text-center"
            aria-current={index === current ? 'step' : undefined}
          >
            <div className="flex w-full items-center">
              <span className={`h-1 flex-1 ${index === 0 ? 'invisible' : done ? 'bg-brand-600' : 'bg-line'}`} />
              <span
                aria-hidden="true"
                className={`grid size-9 shrink-0 place-items-center rounded-full text-base ${
                  done ? 'bg-brand-600 text-white' : 'bg-canvas text-ink-muted'
                } ${index === current ? 'ring-brand-200 ring-4' : ''}`}
              >
                {STATUS[step].icon}
              </span>
              <span
                className={`h-1 flex-1 ${index === JOURNEY.length - 1 ? 'invisible' : index < current ? 'bg-brand-600' : 'bg-line'}`}
              />
            </div>
            <span className={`mt-1 text-xs ${done ? 'text-ink font-semibold' : 'text-ink-muted'}`}>
              {STATUS[step].short}
              <span className="sr-only">{done ? ' (done)' : ' (pending)'}</span>
            </span>
          </li>
        )
      })}
    </ol>
  )
}
