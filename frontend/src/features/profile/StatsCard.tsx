import { Meter } from '@/components/shared/Meter'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useLocale } from '@/lib/i18n/locale-context'
import type { QuestionType, StatsDto } from '@/types/api'

// GET /api/me/stats: accuracy and average time per question type (only types answered), plus learning totals.
export function StatsCard({ stats, className }: { stats: StatsDto; className?: string }) {
  const { t } = useLocale()
  const copy = t.profile.statsCard
  const types = Object.keys(stats.accuracyByType) as QuestionType[]
  const totals: [string, number | string][] = [
    [copy.record, `${stats.wins} / ${stats.losses} / ${stats.draws}`],
    [copy.brilliant, stats.brilliantCount],
    [copy.patched, stats.conceptsPatched],
    [copy.graduated, stats.conceptsGraduated],
  ]

  return (
    <Card className={className}>
      <CardHeader>
        <CardTitle>
          <h2>{copy.title}</h2>
        </CardTitle>
        <CardDescription>
          {stats.mostCommonBlunderType ? copy.blunders(t.questionTypes[stats.mostCommonBlunderType]) : copy.subtitle}
        </CardDescription>
      </CardHeader>
      <CardContent className="grid gap-5">
        <dl className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          {totals.map(([label, value]) => (
            <div key={label} className="flex flex-col-reverse gap-1 rounded-lg bg-secondary p-3">
              <dt className="text-xs text-muted-foreground">{label}</dt>
              <dd className="font-heading text-xl font-semibold tabular-nums">{value}</dd>
            </div>
          ))}
        </dl>
        {types.length ? (
          <ul className="grid gap-3 sm:grid-cols-2">
            {types.map((type) => {
              const accuracy = Math.round(stats.accuracyByType[type] ?? 0)
              const time = stats.avgTimeMsByType[type]
              return (
                <li key={type} className="grid gap-1.5">
                  <p className="flex items-baseline gap-2 text-sm">
                    <span className="min-w-0 flex-1 truncate font-medium">{t.questionTypes[type]}</span>
                    <span className="font-mono text-xs text-muted-foreground tabular-nums">
                      {accuracy}%{time != null && ` · ${(time / 1000).toFixed(1)} s`}
                    </span>
                  </p>
                  <Meter value={accuracy / 100} label={copy.accuracy(t.questionTypes[type])} />
                </li>
              )
            })}
          </ul>
        ) : (
          <p className="text-sm text-muted-foreground">{copy.empty}</p>
        )}
      </CardContent>
    </Card>
  )
}
