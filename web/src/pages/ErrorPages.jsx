import { Link, isRouteErrorResponse, useRouteError } from 'react-router'

function ErrorLayout({ title, message }) {
  return (
    <div className="mx-auto max-w-md py-16 text-center">
      <h1 className="text-2xl font-bold">{title}</h1>
      <p className="text-ink-muted mt-2">{message}</p>
      <Link to="/" className="text-brand-700 mt-6 inline-block font-semibold underline underline-offset-4">
        Back to the store
      </Link>
    </div>
  )
}

export function NotFoundPage() {
  return <ErrorLayout title="Page not found" message="The page you are looking for does not exist." />
}

/** Catches errors thrown while rendering any route, so one broken page never blanks the whole app. */
export function RouteErrorPage() {
  const error = useRouteError()
  if (isRouteErrorResponse(error) && error.status === 404) return <NotFoundPage />
  return <ErrorLayout title="Something went wrong" message="Please refresh the page or try again in a moment." />
}
