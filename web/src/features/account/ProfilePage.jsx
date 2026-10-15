import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Button } from '../../shared/ui/Button'
import { Alert, ErrorAlert } from '../../shared/ui/Alert'
import { Card } from '../../shared/ui/Card'
import { TextField } from '../../shared/ui/forms'
import { applyServerErrors, formLevelError } from '../../shared/api/formErrors'
import { mobileField, nameField, newPasswordField } from '../../shared/validation/schemas'
import { useCurrentUser } from '../auth/useAuth'
import { useChangePassword, useUpdateProfile } from './accountApi'

const profileSchema = z.object({ fullName: nameField(), phoneNumber: mobileField })

function ProfileForm({ user }) {
  const update = useUpdateProfile()
  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors, isDirty },
  } = useForm({
    resolver: zodResolver(profileSchema),
    defaultValues: { fullName: user.fullName, phoneNumber: user.phoneNumber ?? '' },
  })

  const onSubmit = (values) =>
    update.mutate(values, {
      onSuccess: (saved) => reset({ fullName: saved.fullName, phoneNumber: saved.phoneNumber }),
      onError: (error) => applyServerErrors(error, setError),
    })

  return (
    <Card title="Personal details">
      <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-4">
        <ErrorAlert error={formLevelError(update.error)} title="Couldn't save your details" />
        {update.isSuccess && !isDirty && <Alert tone="success">Your details have been saved.</Alert>}
        <TextField label="Email" value={user.email} disabled hint="Contact the store to change your email." readOnly />
        <TextField label="Full name" autoComplete="name" error={errors.fullName} {...register('fullName')} />
        <TextField
          label="Mobile number"
          type="tel"
          inputMode="numeric"
          autoComplete="tel-national"
          error={errors.phoneNumber}
          {...register('phoneNumber')}
        />
        <Button type="submit" disabled={!isDirty || update.isPending}>
          {update.isPending ? 'Saving…' : 'Save changes'}
        </Button>
      </form>
    </Card>
  )
}

const passwordSchema = z
  .object({
    currentPassword: z.string().min(1, 'Enter your current password.'),
    newPassword: newPasswordField,
    confirmPassword: z.string(),
  })
  .refine((v) => v.newPassword === v.confirmPassword, {
    path: ['confirmPassword'],
    message: "The passwords don't match.",
  })

function ChangePasswordForm() {
  const change = useChangePassword()
  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(passwordSchema),
    defaultValues: { currentPassword: '', newPassword: '', confirmPassword: '' },
  })

  const onSubmit = ({ currentPassword, newPassword }) =>
    change.mutate(
      { currentPassword, newPassword },
      {
        onSuccess: () => reset(),
        onError: (error) => applyServerErrors(error, setError),
      },
    )

  return (
    <Card title="Change password" description="You'll stay signed in here; other devices will be signed out.">
      <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-4">
        <ErrorAlert error={formLevelError(change.error)} title="Couldn't change your password" />
        {change.isSuccess && <Alert tone="success">Your password has been changed.</Alert>}
        <TextField
          label="Current password"
          type="password"
          autoComplete="current-password"
          error={errors.currentPassword}
          {...register('currentPassword')}
        />
        <TextField
          label="New password"
          type="password"
          autoComplete="new-password"
          error={errors.newPassword}
          {...register('newPassword')}
        />
        <TextField
          label="Confirm new password"
          type="password"
          autoComplete="new-password"
          error={errors.confirmPassword}
          {...register('confirmPassword')}
        />
        <Button type="submit" variant="secondary" disabled={change.isPending}>
          {change.isPending ? 'Changing…' : 'Change password'}
        </Button>
      </form>
    </Card>
  )
}

export function ProfilePage() {
  const { data: user } = useCurrentUser() // RequireAuth guarantees a user here
  return (
    <div className="space-y-6">
      <ProfileForm user={user} />
      <ChangePasswordForm />
    </div>
  )
}
