import type { ReactNode } from 'react'
import { cn } from '@/utils/cn'

export function Badge({ children, tone = 'neutral', className }: {
  children: ReactNode
  tone?: 'neutral' | 'success' | 'warning' | 'danger' | 'info'
  className?: string
}) {
  return <span className={cn('badge', `badge--${tone}`, className)}>{children}</span>
}
