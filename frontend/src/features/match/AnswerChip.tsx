import { Check, Minus, Sparkles, X } from 'lucide-react'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import type { AnswerLabel, AnswerResultDto } from '@/types/api'

const LABEL_STYLES: Record<AnswerLabel, { icon: typeof Check; className: string }> = {
  brilliant: { icon: Sparkles, className: 'bg-primary/15 text-primary' },
  good: { icon: Check, className: 'bg-success/15 text-success' },
  miss: { icon: Minus, className: 'bg-secondary text-muted-foreground' },
  blunder: { icon: X, className: 'bg-destructive/15 text-destructive' },
}

export function AnswerChip({ who, result }: { who?: string; result: AnswerResultDto }) {
  const { t } = useLocale()
  const { icon: Icon, className } = LABEL_STYLES[result.label]

  return (
    <span className={cn('inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-sm font-medium', className)}>
      <Icon aria-hidden="true" className="size-4" />
      {who && `${who}: `}
      {result.timedOut ? t.match.timedOut : t.match.labels[result.label]}
      <span className="font-mono text-xs">+{result.points}</span>
    </span>
  )
}
