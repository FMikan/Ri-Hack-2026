import { DeltaBadge } from '@/components/shared/DeltaBadge'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { PLACEMENT_MATCHES } from '@/lib/game'
import { useLocale } from '@/lib/i18n/locale-context'
import type { DashboardDto, MeDto } from '@/types/api'
import { RatingChart } from './RatingChart'

// Rating is the headline number of the page; the chip is the change across the plotted matches.
export function RatingCard({ me, history, className }: { me: MeDto; history: DashboardDto['ratingHistory']; className?: string }) {
  const { t } = useLocale()
  const copy = t.dashboard.rating
  const change = history.length >= 2 ? history[history.length - 1].rating - history[0].rating : undefined

  return (
    <Card className={className}>
      <CardHeader>
        <CardTitle>
          <h2>{copy.title}</h2>
        </CardTitle>
        <CardDescription>{copy.subtitle}</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-6">
        <div className="grid gap-1.5">
          <p className="flex flex-wrap items-center gap-3">
            <span className="font-heading text-4xl font-semibold tracking-tight">{me.rating}</span>
            {change ? (
              <DeltaBadge value={change} />
            ) : (
              me.isProvisional && <Badge variant="outline">{copy.provisional}</Badge>
            )}
          </p>
          <p className="text-sm text-muted-foreground">
            {me.isProvisional
              ? copy.placement(Math.min(me.rankedMatches, PLACEMENT_MATCHES), PLACEMENT_MATCHES)
              : copy.matches(me.rankedMatches)}
          </p>
        </div>
        <RatingChart points={history} />
      </CardContent>
    </Card>
  )
}
