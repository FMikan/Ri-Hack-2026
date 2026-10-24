import { cn } from '@/lib/utils'

export type StatusTone = 'ok' | 'pending' | 'error'

const toneClasses: Record<StatusTone, string> = {
  ok: 'bg-success',
  pending: 'bg-warning animate-pulse motion-reduce:animate-none',
  error: 'bg-destructive',
}

// The text label carries the meaning; the dot is decoration only.
export function StatusIndicator({ tone, label }: { tone: StatusTone; label: string }) {
  return (
    <span className="inline-flex items-center gap-2 font-mono text-xs font-medium uppercase tracking-wide">
      <span aria-hidden="true" className={cn('size-2 rounded-full', toneClasses[tone])} />
      {label}
    </span>
  )
}
