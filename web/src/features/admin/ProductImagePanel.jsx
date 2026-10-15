import { useRef } from 'react'
import { Button } from '../../shared/ui/Button'
import { ErrorAlert } from '../../shared/ui/Alert'
import { Card } from '../../shared/ui/Card'
import { useRemoveProductImage, useUploadProductImage } from './adminCatalogApi'

const MAX_BYTES = 2 * 1024 * 1024
const ACCEPTED = 'image/jpeg,image/png,image/webp'

export function ProductImagePanel({ product }) {
  const input = useRef(null)
  const upload = useUploadProductImage()
  const remove = useRemoveProductImage()

  const onFileChosen = (event) => {
    const file = event.target.files?.[0]
    event.target.value = '' // allow choosing the same file again after an error
    if (!file) return
    if (file.size > MAX_BYTES) {
      upload.reset()
      window.alert('Please choose an image smaller than 2 MB.')
      return
    }
    upload.mutate({ id: product.id, file })
  }

  const error = upload.error ?? remove.error
  const fileError = error?.errors?.file?.[0]

  return (
    <Card title="Photo">
      {product.imageUrl ? (
        <img
          src={product.imageUrl}
          alt={product.name}
          className="bg-canvas aspect-square w-full rounded-lg object-contain"
        />
      ) : (
        <div className="bg-canvas text-ink-muted grid aspect-square w-full place-items-center rounded-lg text-sm">
          No photo yet
        </div>
      )}
      <div className="mt-3 space-y-2">
        {fileError ? (
          <ErrorAlert error={{ message: fileError }} title="Couldn't use this image" />
        ) : (
          <ErrorAlert error={error} />
        )}
        <input ref={input} type="file" accept={ACCEPTED} className="sr-only" onChange={onFileChosen} tabIndex={-1} />
        <div className="flex gap-2">
          <Button size="sm" disabled={upload.isPending} onClick={() => input.current?.click()}>
            {upload.isPending ? 'Uploading…' : product.imageUrl ? 'Replace photo' : 'Upload photo'}
          </Button>
          {product.imageUrl && (
            <Button
              size="sm"
              variant="ghost"
              className="text-danger-700"
              disabled={remove.isPending}
              onClick={() => window.confirm('Remove this photo?') && remove.mutate(product.id)}
            >
              Remove
            </Button>
          )}
        </div>
        <p className="text-ink-muted text-xs">
          JPEG, PNG or WebP, up to 2 MB. A square photo on a plain background looks best.
        </p>
      </div>
    </Card>
  )
}
