import { Link, useNavigate } from 'react-router'
import { Button } from '../../shared/ui/Button'
import { isAdmin, useCurrentUser, useLogout } from './useAuth'

/** Header area: "Sign in" for visitors; account, admin and sign-out links for signed-in users. */
export function AccountNav() {
  const { data: user, isPending } = useCurrentUser()
  const logout = useLogout()
  const navigate = useNavigate()

  if (isPending) return <div className="h-9 w-20" aria-hidden="true" />

  if (!user) {
    return (
      <Link
        to="/login"
        className="bg-brand-600 hover:bg-brand-700 inline-flex h-9 items-center rounded-lg px-4 text-sm font-semibold text-white"
      >
        Sign in
      </Link>
    )
  }

  const firstName = user.fullName.split(' ')[0]

  return (
    <nav aria-label="Account" className="flex items-center gap-1 text-sm sm:gap-2">
      {isAdmin(user) && (
        <Link to="/admin/customers" className="text-ink-muted hover:text-ink rounded-lg px-2 py-2 font-semibold">
          Admin
        </Link>
      )}
      <Link
        to="/account"
        className="hover:bg-brand-50 flex items-center gap-2 rounded-lg px-2 py-2 font-semibold"
        aria-label={`My account (${user.fullName})`}
      >
        <span
          aria-hidden="true"
          className="bg-brand-100 text-brand-900 grid size-7 place-items-center rounded-full text-xs font-bold"
        >
          {firstName[0]?.toUpperCase()}
        </span>
        <span className="hidden max-w-32 truncate sm:inline">{firstName}</span>
      </Link>
      <Button
        size="sm"
        variant="ghost"
        disabled={logout.isPending}
        onClick={() => logout.mutate(undefined, { onSettled: () => navigate('/') })}
      >
        Sign out
      </Button>
    </nav>
  )
}
