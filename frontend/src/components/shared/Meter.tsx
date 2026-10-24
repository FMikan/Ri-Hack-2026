import { cn } from '@/lib/utils'

interface MeterProps {
  value: number
  label: string
  // accent: a measurement (mastery); progress: distance to a goal (next tier, next level), green to red as it gets close.
  tone?: 'accent' | 'progress'
  className?: string
}

// One value from 0 to 1 as a bar on a raised track. The fill spans the whole track and is clipped to the value, so
// the progress gradient shows more of its red end the closer the goal is.
export function Meter({ value, label, tone = 'accent', className }: MeterProps) {
  const percent = Math.round(Math.min(1, Math.max(0, value)) * 100)
  return (
    <div
      role="meter"
      aria-label={label}
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={percent}
      className={cn('h-2 overflow-hidden rounded-full bg-secondary', className)}
    >
      <div
        className={cn('h-full', tone === 'progress' ? 'bg-linear-to-r from-success via-warning to-destructive' : 'bg-chart-1')}
        style={{ clipPath: `inset(0 ${100 - percent}% 0 0 round 9999px)` }}
      />
    </div>
  )
}
