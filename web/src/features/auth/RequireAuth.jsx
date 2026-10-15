import { Navigate, Outlet, useLocation } from 'react-router'
import { Spinner } from '../../shared/ui/Spinner'
import { useCurrentUser } from './useAuth'
import { ForbiddenPage } from '../../pages/ErrorPages'

/**
 * Route guard. Sends anonymous visitors to the login page (remembering where they were going) and
 * shows "access denied" to signed-in users without the required role.
 * This is a convenience for the UI only: the API enforces the same rules on every request.
 */
export function RequireAuth({ role }) {
  const { data: user, isPending } = useCurrentUser()
  const location = useLocation()

  if (isPending) return <Spinner />

  if (!user) {
    const returnTo = encodeURIComponent(location.pathname + location.search)
    return <Navigate to={`/login?returnTo=${returnTo}`} replace />
  }

  if (role && !user.roles.includes(role)) return <ForbiddenPage />

  return <Outlet />
}
