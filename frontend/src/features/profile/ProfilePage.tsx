import { UserAvatar } from '@/components/shared/UserAvatar'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { SkillGraphCard } from '@/features/dashboard/SkillGraphCard'
import { useAsync } from '@/hooks/use-async'
import { api } from '@/lib/api-client'
import { bannerClass, cardClass, nameClass } from '@/lib/cosmetics'
import { monthYear } from '@/lib/format'
import { PLACEMENT_MATCHES } from '@/lib/game'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import type { KnowledgeMapDto, MatchSummaryDto, MeDto } from '@/types/api'
import { KnowledgeMapCard } from './KnowledgeMapCard'
import { MatchHistoryCard } from './MatchHistoryCard'

const loadProfile = (signal: AbortSignal) => Promise.all([api.me(signal), api.knowledgeMap(signal), api.myMatches(signal)])

// Same three-column grid as the dashboard.
const GRID = 'grid gap-4 lg:gap-5 xl:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_minmax(0,1.15fr)]'

// The player's own profile: who they are, the skill graph, the knowledge map per concept and the match history.
// Titles, achievements and detailed stats come with F5.
export function ProfilePage() {
  const { t } = useLocale()
  const profile = useAsync(loadProfile)

  return (
    <main className="mx-auto grid w-full max-w-7xl content-start gap-5 px-4 pb-10 sm:px-6 lg:px-8">
      <title>{`${t.profile.documentTitle} · StackOff`}</title>
      {profile.data ? (
        <ProfileView me={profile.data[0]} map={profile.data[1]} history={profile.data[2]} />
      ) : profile.error ? (
        <Card role="alert">
          <CardContent className="grid justify-items-start gap-3">
            <p>{t.profile.loadFailed}</p>
            <p className="text-sm text-muted-foreground">{profile.error.message}</p>
            <Button variant="outline" onClick={profile.reload}>
              {t.common.retry}
            </Button>
          </CardContent>
        </Card>
      ) : (
        <div aria-busy="true" className={GRID}>
          <p role="status" className="sr-only">
            {t.common.loading}
          </p>
          <Skeleton className="h-52 rounded-xl xl:col-span-2" />
          <Skeleton className="h-52 rounded-xl" />
          <Skeleton className="h-96 rounded-xl xl:col-span-3" />
        </div>
      )}
    </main>
  )
}

function ProfileView({ me, map, history }: { me: MeDto; map: KnowledgeMapDto; history: MatchSummaryDto[] }) {
  return (
    <div className={GRID}>
      <ProfileCard me={me} history={history} className="xl:col-span-2" />
      <SkillGraphCard topics={map.topics} className="xl:col-span-2 xl:row-start-2" />
      <MatchHistoryCard history={history} className="xl:col-start-3 xl:row-span-2 xl:row-start-1" />
      <KnowledgeMapCard map={map} className="xl:col-span-3" />
    </div>
  )
}

function ProfileCard({ me, history, className }: { me: MeDto; history: MatchSummaryDto[]; className?: string }) {
  const { t, locale } = useLocale()
  const copy = t.profile
  const duels = history.filter((match) => match.opponent)
  const wins = duels.filter((match) => match.outcome === 'win').length
  const stats: [string, string | number][] = [
    [copy.stats.duels, duels.length],
    [copy.stats.wins, wins],
    [copy.stats.winRate, duels.length ? `${Math.round((wins / duels.length) * 100)}%` : '–'],
    [copy.stats.streak, me.streak.count],
  ]

  return (
    <Card className={cn(className, cardClass(me))}>
      <CardContent className="grid gap-6">
        {bannerClass(me) && <div aria-hidden="true" className={cn('-mb-2 h-14 rounded-lg', bannerClass(me))} />}
        <div className="flex flex-wrap items-center gap-4">
          <UserAvatar user={me} className="size-16" />
          <div className="grid min-w-0 gap-1">
            <h1 className={cn('truncate font-heading text-2xl font-semibold tracking-tight sm:text-3xl', nameClass(me))}>{me.username}</h1>
            {me.title && <p className="text-sm font-medium text-primary">{me.title}</p>}
            <p className="text-sm text-muted-foreground">{copy.since(monthYear(me.createdAt, locale))}</p>
          </div>
          <div className="flex flex-wrap gap-2 sm:ml-auto">
            <Badge variant="secondary">{t.dashboard.tiers[me.tier]}</Badge>
            <Badge variant="secondary">
              {me.isProvisional ? copy.placement(me.rankedMatches, PLACEMENT_MATCHES) : copy.rating(me.rating)}
            </Badge>
            <Badge variant="secondary">{t.dashboard.progress.levelValue(me.level)}</Badge>
          </div>
        </div>
        <dl className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          {stats.map(([label, value]) => (
            <div key={label} className="flex flex-col-reverse gap-1 rounded-lg bg-secondary p-4">
              <dt className="text-sm text-muted-foreground">{label}</dt>
              <dd className="font-heading text-2xl font-semibold tabular-nums sm:text-3xl">{value}</dd>
            </div>
          ))}
        </dl>
      </CardContent>
    </Card>
  )
}
