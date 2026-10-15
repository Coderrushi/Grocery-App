import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Link, Navigate, useNavigate, useSearchParams } from 'react-router'
import { Button } from '../../shared/ui/Button'
import { ErrorAlert } from '../../shared/ui/Alert'
import { TextField } from '../../shared/ui/forms'
import { applyServerErrors, formLevelError } from '../../shared/api/formErrors'
import { emailField, mobileField, nameField, newPasswordField } from '../../shared/validation/schemas'
import { AuthPageShell } from './AuthPageShell'
import { useCurrentUser, useRegister } from './useAuth'
import { safeReturnPath } from './safeReturnPath'

const schema = z.object({
  fullName: nameField(),
  phoneNumber: mobileField,
  email: emailField,
  password: newPasswordField,
})

export function RegisterPage() {
  const [params] = useSearchParams()
  const returnTo = safeReturnPath(params.get('returnTo'))
  const navigate = useNavigate()
  const { data: user } = useCurrentUser()
  const registration = useRegister()

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(schema),
    defaultValues: { fullName: '', phoneNumber: '', email: '', password: '' },
  })

  if (user && !registration.isSuccess) return <Navigate to={returnTo} replace />

  const onSubmit = (values) =>
    registration.mutate(values, {
      onSuccess: () => navigate(returnTo, { replace: true }),
      onError: (error) => applyServerErrors(error, setError),
    })

  return (
    <AuthPageShell
      title="Create your account"
      description="Fresh groceries delivered to your doorstep."
      footer={
        <>
          Already have an account?{' '}
          <Link to={`/login?returnTo=${encodeURIComponent(returnTo)}`} className="text-brand-700 font-semibold">
            Sign in
          </Link>
        </>
      }
    >
      <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-4">
        <ErrorAlert error={formLevelError(registration.error)} title="Couldn't create your account" />
        <TextField label="Full name" autoComplete="name" autoFocus error={errors.fullName} {...register('fullName')} />
        <TextField
          label="Mobile number"
          type="tel"
          inputMode="numeric"
          autoComplete="tel-national"
          hint="We'll call this number if we need to reach you about a delivery."
          error={errors.phoneNumber}
          {...register('phoneNumber')}
        />
        <TextField label="Email" type="email" autoComplete="email" error={errors.email} {...register('email')} />
        <TextField
          label="Password"
          type="password"
          autoComplete="new-password"
          hint="At least 8 characters. A short phrase is easy to remember and hard to guess."
          error={errors.password}
          {...register('password')}
        />
        <Button type="submit" className="w-full" disabled={registration.isPending}>
          {registration.isPending ? 'Creating account…' : 'Create account'}
        </Button>
      </form>
    </AuthPageShell>
  )
}
