import { Award, Check } from 'lucide-react'
import { useState } from 'react'
import { Meter } from '@/components/shared/Meter'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useAuth } from '@/features/auth/auth-context'
import { useAsync } from '@/hooks/use-async'
import { api } from '@/lib/api-client'
import { relativeTime } from '@/lib/format'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import type { TitleDto } from '@/types/api'

const loadHonors = (signal: AbortSignal) => Promise.all([api.titles(signal), api.achievements(signal)])

// Titles (one can be worn next to the name everywhere) and the nine achievements with their progress (game idea §14).
export function HonorsCard({ className }: { className?: string }) {
  const { t, locale } = useLocale()
  const copy = t.profile.honors
  const honors = useAsync(loadHonors)

  return (
    <Card className={className}>
      <CardHeader>
        <CardTitle>
          <h2>{copy.title}</h2>
        </CardTitle>
        <CardDescription>{copy.subtitle}</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-5">
        {honors.data ? (
          <>
            <section className="grid gap-2">
              <h3 className="text-sm font-medium text-muted-foreground">{copy.titles}</h3>
              {honors.data[0].length ? (
                <ul className="grid gap-2 sm:grid-cols-2">
                  {honors.data[0].map((title) => (
                    <TitleRow key={title.id} title={title} />
                  ))}
                </ul>
              ) : (
                <p className="text-sm text-muted-foreground">{copy.noTitles}</p>
              )}
            </section>
            <section className="grid gap-2">
              <h3 className="text-sm font-medium text-muted-foreground">{copy.achievements}</h3>
              <ul className="grid gap-2 sm:grid-cols-2 xl:grid-cols-3">
                {honors.data[1].map((achievement) => (
                  <li
                    key={achievement.key}
                    className={cn(
                      'grid gap-2 rounded-lg p-3 ring-1 ring-inset',
                      achievement.earnedAt ? 'bg-warning/10 ring-warning/40' : 'bg-secondary ring-border',
                    )}
                  >
                    <p className="flex items-center gap-2 text-sm font-medium">
                      <Award aria-hidden="true" className={cn('size-4 shrink-0', achievement.earnedAt ? 'text-warning' : 'text-muted-foreground')} />
                      {t.achievements[achievement.key as keyof typeof t.achievements] ?? achievement.name}
                    </p>
                    {achievement.earnedAt ? (
                      <p className="text-xs text-muted-foreground">{copy.earned(relativeTime(achievement.earnedAt, locale))}</p>
                    ) : (
                      achievement.target != null && (
                        <div className="grid gap-1">
                          <Meter value={(achievement.progress ?? 0) / achievement.target} label={achievement.name} />
                          <p className="font-mono text-xs text-muted-foreground tabular-nums">
                            {Math.min(achievement.progress ?? 0, achievement.target)}/{achievement.target}
                          </p>
                        </div>
                      )
                    )}
                  </li>
                ))}
              </ul>
            </section>
          </>
        ) : honors.error ? (
          <p role="alert" className="text-sm text-destructive">
            {honors.error.message}
          </p>
        ) : (
          <Skeleton aria-busy="true" className="h-48 rounded-lg" />
        )}
      </CardContent>
    </Card>
  )
}

// PUT /api/me/equipment { titleId }: the title shows next to my name; a lost knowledge title cannot be worn.
function TitleRow({ title }: { title: TitleDto }) {
  const { t } = useLocale()
  const copy = t.profile.honors
  const { session, setUser } = useAuth()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string>()
  const worn = session.status === 'authenticated' && session.user.equippedTitleId === title.id

  async function toggle() {
    setBusy(true)
    setError(undefined)
    try {
      setUser(await api.equip({ titleId: worn ? null : title.id }))
    } catch (caught) {
      setError(String((caught as Error).message))
    } finally {
      setBusy(false)
    }
  }

  return (
    <li className="flex flex-wrap items-center gap-2 rounded-lg px-3 py-2.5 ring-1 ring-border ring-inset">
      <span className={cn('min-w-0 flex-1 text-sm font-medium', !title.active && 'text-muted-foreground line-through')}>{title.name}</span>
      {title.count > 1 && <Badge variant="secondary">×{title.count}</Badge>}
      {title.active ? (
        <Button size="sm" variant={worn ? 'outline' : 'ghost'} aria-disabled={busy} onClick={() => !busy && toggle()}>
          {worn && <Check aria-hidden="true" />}
          {worn ? copy.worn : copy.wear}
        </Button>
      ) : (
        <span className="text-xs text-muted-foreground">{copy.lost}</span>
      )}
      {error && (
        <p role="alert" className="basis-full text-xs text-destructive">
          {error}
        </p>
      )}
    </li>
  )
}
