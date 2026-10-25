import type { ReactNode } from 'react'
import { Meter } from '@/components/shared/Meter'
import { TierIcon } from '@/components/shared/TierIcon'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent } from '@/components/ui/card'
import { levelProgress, tierProgress } from '@/lib/game'
import { useLocale } from '@/lib/i18n/locale-context'
import type { MeDto } from '@/types/api'

interface ProgressTileProps {
  icon?: ReactNode
  value: string
  chip?: ReactNode
  label: string
  goal: string
  progress: number
}

// Big white value, muted label, then the distance to the next goal with a green bar (the reference's "to Goal" tile).
function ProgressTile({ icon, value, chip, label, goal, progress }: ProgressTileProps) {
  return (
    <Card>
      <CardContent className="flex h-full flex-col gap-1.5">
        <p className="flex flex-wrap items-center gap-3">
          <span className="font-heading text-xl font-semibold tracking-tight sm:text-3xl">{value}</span>
          {icon}
          {chip}
        </p>
        <p className="text-sm text-muted-foreground">{label}</p>
        <div className="mt-auto grid gap-2.5 pt-6 sm:pt-8">
          <p className="flex items-baseline justify-between gap-x-3 text-xs sm:text-sm">
            <span className="text-foreground/80">{goal}</span>
            <span className="font-mono text-xs text-muted-foreground tabular-nums">{Math.round(progress * 100)}%</span>
          </p>
          <Meter value={progress} label={goal} tone="progress" />
        </div>
      </CardContent>
    </Card>
  )
}

export function TierTile({ me }: { me: MeDto }) {
  const { t } = useLocale()
  const copy = t.dashboard.progress
  const { next, remaining, progress } = tierProgress(me.tier, me.rating)

  return (
    <ProgressTile
      icon={<TierIcon tier={me.tier} className="size-7 sm:size-9" />}
      value={t.dashboard.tiers[me.tier]}
      label={copy.tier}
      goal={next ? copy.toNext(remaining, t.dashboard.tiers[next]) : copy.top}
      progress={progress}
    />
  )
}

export function LevelTile({ me }: { me: MeDto }) {
  const { t } = useLocale()
  const copy = t.dashboard.progress

  return (
    <ProgressTile
      value={copy.levelValue(me.level)}
      chip={<Badge variant="secondary">{copy.xp(me.xp)}</Badge>}
      label={copy.level}
      goal={copy.toNextLevel(me.xpToNextLevel, me.level + 1)}
      progress={levelProgress(me.level, me.xpToNextLevel)}
    />
  )
}
