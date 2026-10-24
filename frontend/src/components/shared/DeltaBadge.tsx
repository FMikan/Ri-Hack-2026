import { ArrowDown, ArrowUp } from 'lucide-react'
import { Badge } from '@/components/ui/badge'

// A change as a small colored chip (↑ 15 green, ↓ 8 red); the sign is spelled out for screen readers.
export function DeltaBadge({ value, className }: { value: number; className?: string }) {
  if (!value) return null
  return (
    <Badge variant={value > 0 ? 'success' : 'destructive'} className={className}>
      {value > 0 ? <ArrowUp aria-hidden="true" /> : <ArrowDown aria-hidden="true" />}
      <span className="sr-only">{value > 0 ? '+' : '-'}</span>
      {Math.abs(value)}
    </Badge>
  )
}
