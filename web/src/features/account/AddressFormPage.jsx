import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useNavigate, useParams } from 'react-router'
import { Button } from '../../shared/ui/Button'
import { ErrorAlert } from '../../shared/ui/Alert'
import { Card } from '../../shared/ui/Card'
import { Spinner } from '../../shared/ui/Spinner'
import { CheckboxField, SelectField, TextField } from '../../shared/ui/forms'
import { applyServerErrors, formLevelError } from '../../shared/api/formErrors'
import { mobileField, nameField, pincodeField } from '../../shared/validation/schemas'
import { INDIAN_STATES } from '../../shared/utils/india'
import { useCurrentUser } from '../auth/useAuth'
import { useAddress, useSaveAddress } from './accountApi'

const LABELS = ['Home', 'Work', 'Other']

const optionalText = (max) =>
  z
    .string()
    .trim()
    .max(max)
    .transform((v) => v || null)

const addressSchema = z.object({
  label: z.enum(LABELS),
  recipientName: nameField("Enter the recipient's name."),
  phoneNumber: mobileField,
  line1: z.string().trim().min(1, 'Enter the house/flat number and building.').max(150),
  line2: optionalText(150),
  landmark: optionalText(100),
  city: z.string().trim().min(1, 'Enter the city.').max(60),
  state: z.string().refine((v) => INDIAN_STATES.includes(v), 'Select a state.'),
  pincode: pincodeField,
  isDefault: z.boolean(),
})

export function AddressForm({ initialValues, onSubmit, isSaving, error, submitLabel }) {
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm({ resolver: zodResolver(addressSchema), defaultValues: initialValues })

  const submit = (values) => onSubmit(values, (serverError) => applyServerErrors(serverError, setError))

  return (
    <form onSubmit={handleSubmit(submit)} noValidate className="space-y-4">
      <ErrorAlert error={formLevelError(error)} title="Couldn't save the address" />

      <fieldset>
        <legend className="mb-2 text-sm font-medium">Save as</legend>
        <div className="flex gap-2">
          {LABELS.map((label) => (
            <label
              key={label}
              className="border-line has-checked:border-brand-600 has-checked:bg-brand-50 has-checked:text-brand-900 flex h-10 cursor-pointer items-center gap-2 rounded-full border px-4 text-sm font-medium"
            >
              <input type="radio" value={label} className="sr-only" {...register('label')} />
              {label}
            </label>
          ))}
        </div>
      </fieldset>

      <div className="grid gap-4 sm:grid-cols-2">
        <TextField
          label="Recipient name"
          autoComplete="name"
          error={errors.recipientName}
          {...register('recipientName')}
        />
        <TextField
          label="Mobile number"
          type="tel"
          inputMode="numeric"
          autoComplete="tel-national"
          error={errors.phoneNumber}
          {...register('phoneNumber')}
        />
      </div>
      <TextField
        label="Flat / house no., building"
        autoComplete="address-line1"
        error={errors.line1}
        {...register('line1')}
      />
      <TextField
        label="Street, area, locality (optional)"
        autoComplete="address-line2"
        error={errors.line2}
        {...register('line2')}
      />
      <TextField
        label="Landmark (optional)"
        placeholder="e.g. Near City Pride"
        error={errors.landmark}
        {...register('landmark')}
      />
      <div className="grid gap-4 sm:grid-cols-3">
        <TextField label="City" autoComplete="address-level2" error={errors.city} {...register('city')} />
        <SelectField label="State" autoComplete="address-level1" error={errors.state} {...register('state')}>
          <option value="">Select…</option>
          {INDIAN_STATES.map((s) => (
            <option key={s} value={s}>
              {s}
            </option>
          ))}
        </SelectField>
        <TextField
          label="PIN code"
          inputMode="numeric"
          autoComplete="postal-code"
          maxLength={6}
          error={errors.pincode}
          {...register('pincode')}
        />
      </div>
      <CheckboxField label="Use as my default delivery address" {...register('isDefault')} />
      <Button type="submit" className="w-full sm:w-auto" disabled={isSaving}>
        {isSaving ? 'Saving…' : submitLabel}
      </Button>
    </form>
  )
}

const emptyAddress = (user) => ({
  label: 'Home',
  recipientName: user?.fullName ?? '',
  phoneNumber: user?.phoneNumber ?? '',
  line1: '',
  line2: '',
  landmark: '',
  city: '',
  state: '',
  pincode: '',
  isDefault: false,
})

const toFormValues = (a) => ({ ...a, line2: a.line2 ?? '', landmark: a.landmark ?? '' })

export function AddressFormPage() {
  const { id } = useParams()
  const addressId = id ? Number(id) : null
  const navigate = useNavigate()
  const { data: user } = useCurrentUser()
  const existing = useAddress(addressId)
  const save = useSaveAddress()

  if (addressId && existing.isPending) return <Spinner />
  if (addressId && existing.error) return <ErrorAlert error={existing.error} title="Couldn't load this address" />

  const onSubmit = (values, showFieldErrors) =>
    save.mutate(
      { id: addressId, ...values },
      { onSuccess: () => navigate('/account/addresses'), onError: showFieldErrors },
    )

  return (
    <Card title={addressId ? 'Edit address' : 'Add a delivery address'}>
      <AddressForm
        key={addressId ?? 'new'}
        initialValues={addressId ? toFormValues(existing.data) : emptyAddress(user)}
        onSubmit={onSubmit}
        isSaving={save.isPending}
        error={save.error}
        submitLabel={addressId ? 'Save address' : 'Add address'}
      />
    </Card>
  )
}
