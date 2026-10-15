import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Link, Navigate, useNavigate, useSearchParams } from 'react-router'
import { Button } from '../../shared/ui/Button'
import { ErrorAlert } from '../../shared/ui/Alert'
import { CheckboxField, TextField } from '../../shared/ui/forms'
import { applyServerErrors, formLevelError } from '../../shared/api/formErrors'
import { AuthPageShell } from './AuthPageShell'
import { useCurrentUser, useLogin } from './useAuth'
import { safeReturnPath } from './safeReturnPath'

const schema = z.object({
  identifier: z.string().trim().min(1, 'Enter your email or mobile number.'),
  password: z.string().min(1, 'Enter your password.'),
  rememberMe: z.boolean(),
})

export function LoginPage() {
  const [params] = useSearchParams()
  const returnTo = safeReturnPath(params.get('returnTo'))
  const navigate = useNavigate()
  const { data: user } = useCurrentUser()
  const login = useLogin()

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(schema),
    defaultValues: { identifier: '', password: '', rememberMe: true },
  })

  if (user && !login.isSuccess) return <Navigate to={returnTo} replace />

  const onSubmit = (values) =>
    login.mutate(values, {
      onSuccess: () => navigate(returnTo, { replace: true }),
      onError: (error) => applyServerErrors(error, setError),
    })

  return (
    <AuthPageShell
      title="Sign in"
      description="Welcome back! Sign in to order and track your groceries."
      footer={
        <>
          New to QuickKirana?{' '}
          <Link to={`/register?returnTo=${encodeURIComponent(returnTo)}`} className="text-brand-700 font-semibold">
            Create an account
          </Link>
        </>
      }
    >
      <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-4">
        <ErrorAlert error={formLevelError(login.error)} title="Couldn't sign you in" />
        <TextField
          label="Email or mobile number"
          autoComplete="username"
          autoFocus
          error={errors.identifier}
          {...register('identifier')}
        />
        <TextField
          label="Password"
          type="password"
          autoComplete="current-password"
          error={errors.password}
          {...register('password')}
        />
        <CheckboxField label="Keep me signed in on this device" {...register('rememberMe')} />
        <Button type="submit" className="w-full" disabled={login.isPending}>
          {login.isPending ? 'Signing in…' : 'Sign in'}
        </Button>
      </form>
    </AuthPageShell>
  )
}
