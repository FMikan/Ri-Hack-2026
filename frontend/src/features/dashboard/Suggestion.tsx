import { Crosshair, RotateCcw, Target, type LucideIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { useStartMatch } from '@/features/match/use-start-match'
import { api } from '@/lib/api-client'
import { useLocale } from '@/lib/i18n/locale-context'
import type { DashboardDto } from '@/types/api'

type Kind = 'review_due' | 'practice_after_losses' | 'calibrate'
const ICONS: Record<Kind, LucideIcon> = { review_due: RotateCcw, practice_after_losses: Target, calibrate: Crosshair }

// The server's one next step (docs §6.11 F2): reviews that are due, practice after losses, or calibrating a topic.
// "play" is what the play card already offers, so it shows nothing here.
export function Suggestion({ data }: { data: DashboardDto }) {
  const { t } = useLocale()
  const copy = t.dashboard.suggestion
  const { start, starting, error } = useStartMatch()
  const { suggestion } = data
  if (!suggestion?.topicId || suggestion.kind === 'play') return null

  const { kind, topicId, conceptIds } = suggestion
  const topic = data.topics.find((entry) => entry.topic.id === topicId)?.topic.name ?? ''
  const Icon = ICONS[kind]
  const run = {
    review_due: () => api.startPractice({ topicId, conceptIds: conceptIds ?? undefined, timed: false }),
    practice_after_losses: () => api.startPractice({ topicId, timed: false }),
    calibrate: () => api.startCalibration({ topicId }),
  }[kind]

  return (
    <section className="flex flex-wrap items-center gap-3 rounded-xl bg-card p-4 ring-1 ring-foreground/10">
      <Icon aria-hidden="true" className="size-5 shrink-0 text-primary" />
      <p className="min-w-0 flex-1 text-sm text-pretty">{copy.text[kind](topic, data.dueReviews)}</p>
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
      <Button variant="outline" aria-disabled={starting} onClick={() => start(run)}>
        {copy.action[kind]}
      </Button>
    </section>
  )
}
