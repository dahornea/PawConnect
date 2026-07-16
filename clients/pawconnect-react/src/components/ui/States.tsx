import { AlertCircle, Dog, LoaderCircle, SearchX } from 'lucide-react'
import type { ReactNode } from 'react'
import { Button } from '@/components/ui/Button'

export function PageLoader({ label = 'Loading' }: { label?: string }) {
  return (
    <div className="state-panel" role="status">
      <LoaderCircle className="state-panel__spinner" aria-hidden="true" />
      <p>{label}...</p>
    </div>
  )
}

export function ErrorState({ title = 'Something went wrong', message, onRetry }: {
  title?: string
  message: string
  onRetry?: () => void
}) {
  return (
    <div className="state-panel state-panel--error" role="alert">
      <AlertCircle aria-hidden="true" />
      <div><h2>{title}</h2><p>{message}</p></div>
      {onRetry && <Button variant="secondary" onClick={onRetry}>Try again</Button>}
    </div>
  )
}

export function EmptyState({ title, message, action }: {
  title: string
  message: string
  action?: ReactNode
}) {
  return (
    <div className="state-panel">
      <SearchX aria-hidden="true" />
      <div><h2>{title}</h2><p>{message}</p></div>
      {action}
    </div>
  )
}

export function DogImageFallback() {
  return <div className="dog-image-fallback"><Dog aria-hidden="true" /><span>No photo available</span></div>
}

export function CardSkeleton() {
  return <div className="card-skeleton" aria-hidden="true"><div /><span /><span /><span /></div>
}
