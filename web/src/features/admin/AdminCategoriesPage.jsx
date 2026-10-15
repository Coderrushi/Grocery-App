import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Button } from '../../shared/ui/Button'
import { ErrorAlert } from '../../shared/ui/Alert'
import { Card } from '../../shared/ui/Card'
import { Spinner } from '../../shared/ui/Spinner'
import { CheckboxField, TextField } from '../../shared/ui/forms'
import { applyServerErrors, formLevelError } from '../../shared/api/formErrors'
import { useAdminCategories, useDeleteCategory, useSaveCategory } from './adminCatalogApi'

const schema = z.object({
  name: z.string().trim().min(1, 'Enter a category name.').max(60),
  icon: z.string().trim().max(16),
  description: z.string().trim().max(300),
  sortOrder: z.number({ error: 'Enter a number.' }).int().min(0).max(1000),
  isActive: z.boolean(),
})

const blank = { name: '', icon: '', description: '', sortOrder: 0, isActive: true }

function CategoryForm({ category, onDone }) {
  const save = useSaveCategory()
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(schema),
    defaultValues: category
      ? { ...category, icon: category.icon ?? '', description: category.description ?? '' }
      : blank,
  })

  const onSubmit = (values) =>
    save.mutate({ id: category?.id, ...values }, { onSuccess: onDone, onError: (e) => applyServerErrors(e, setError) })

  return (
    <Card title={category ? `Edit “${category.name}”` : 'New category'}>
      <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-4">
        <ErrorAlert error={formLevelError(save.error)} title="Couldn't save the category" />
        <TextField label="Name" error={errors.name} {...register('name')} />
        <div className="grid grid-cols-2 gap-4">
          <TextField label="Icon (emoji)" placeholder="🥛" error={errors.icon} {...register('icon')} />
          <TextField
            label="Display order"
            type="number"
            hint="Lower numbers appear first."
            error={errors.sortOrder}
            {...register('sortOrder', { valueAsNumber: true })}
          />
        </div>
        <TextField label="Description (optional)" error={errors.description} {...register('description')} />
        <CheckboxField label="Visible to customers" {...register('isActive')} />
        <div className="flex gap-2">
          <Button type="submit" disabled={save.isPending}>
            {save.isPending ? 'Saving…' : 'Save'}
          </Button>
          <Button variant="secondary" onClick={onDone}>
            Cancel
          </Button>
        </div>
      </form>
    </Card>
  )
}

export function AdminCategoriesPage() {
  const { data: categories, isPending, error } = useAdminCategories()
  const remove = useDeleteCategory()
  // null = no form open, 'new' = create form, or the category being edited.
  const [editing, setEditing] = useState(null)

  const onDelete = (category) => {
    if (window.confirm(`Delete the category “${category.name}”?`)) remove.mutate(category.id)
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between gap-4">
        <h1 className="text-2xl font-bold">Categories</h1>
        {!editing && <Button onClick={() => setEditing('new')}>+ New category</Button>}
      </div>

      {editing && (
        <CategoryForm
          key={editing === 'new' ? 'new' : editing.id}
          category={editing === 'new' ? null : editing}
          onDone={() => setEditing(null)}
        />
      )}

      <ErrorAlert error={remove.error} title="Couldn't delete the category" />
      {isPending && <Spinner />}
      {error && <ErrorAlert error={error} title="Couldn't load categories" />}

      {categories && (
        <div className="rounded-card border-line bg-surface overflow-x-auto border">
          <table className="w-full text-left text-sm">
            <thead className="bg-canvas text-ink-muted">
              <tr>
                <th className="px-4 py-3 font-semibold">Category</th>
                <th className="px-4 py-3 font-semibold">Products</th>
                <th className="px-4 py-3 font-semibold">Order</th>
                <th className="px-4 py-3 font-semibold">Status</th>
                <th className="px-4 py-3">
                  <span className="sr-only">Actions</span>
                </th>
              </tr>
            </thead>
            <tbody className="divide-line divide-y">
              {categories.map((c) => (
                <tr key={c.id}>
                  <td className="px-4 py-3 font-medium">
                    <span aria-hidden="true">{c.icon} </span>
                    {c.name}
                    <span className="text-ink-muted block text-xs">/{c.slug}</span>
                  </td>
                  <td className="px-4 py-3">{c.productCount}</td>
                  <td className="px-4 py-3">{c.sortOrder}</td>
                  <td className="px-4 py-3">
                    {c.isActive ? (
                      <span className="text-success-600 font-semibold">Visible</span>
                    ) : (
                      <span className="text-ink-muted font-semibold">Hidden</span>
                    )}
                  </td>
                  <td className="px-4 py-3 text-right whitespace-nowrap">
                    <Button size="sm" variant="ghost" onClick={() => setEditing(c)}>
                      Edit
                    </Button>
                    <Button
                      size="sm"
                      variant="ghost"
                      className="text-danger-700"
                      disabled={remove.isPending}
                      onClick={() => onDelete(c)}
                    >
                      Delete
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
