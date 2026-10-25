import { Pencil } from 'lucide-react'
import { useEffect, useState } from 'react'
import { SegmentedControl } from '@/components/shared/SegmentedControl'
import { UserAvatar } from '@/components/shared/UserAvatar'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useAuth } from '@/features/auth/auth-context'
import { SkillGraphCard } from '@/features/dashboard/SkillGraphCard'
import { useAsync } from '@/hooks/use-async'
import { api } from '@/lib/api-client'
import { bannerClass, cardClass, nameClass } from '@/lib/cosmetics'
import { monthYear } from '@/lib/format'
import { PLACEMENT_MATCHES } from '@/lib/game'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import type { KnowledgeMapDto, MatchSummaryDto, MeDto, ProfileVisibility, StatsDto } from '@/types/api'
import { AvatarDialog } from './AvatarDialog'
import { HonorsCard } from './HonorsCard'
import { KnowledgeMapCard } from './KnowledgeMapCard'
import { LockerCard } from './LockerCard'
import { MatchHistoryCard } from './MatchHistoryCard'
import { StatsCard } from './StatsCard'

const loadProfile = (signal: AbortSignal) =>
  Promise.all([api.me(signal), api.knowledgeMap(signal), api.myMatches(signal), api.stats(signal)])

// Same three-column grid as the dashboard.
const GRID = 'grid gap-4 lg:gap-5 xl:grid-cols-[minmax(0,1fr)_minmax(0,1fr)_minmax(0,1.15fr)]'

// The player's own profile: who they are, the skill graph, the match history, the locker of owned cosmetics, the
// knowledge map per concept, stats per question type, titles and achievements.
export function ProfilePage() {
  const { t } = useLocale()
  const profile = useAsync(loadProfile)

  return (
    <main className="mx-auto grid w-full max-w-7xl content-start gap-5 px-4 pb-10 sm:px-6 lg:px-8">
      <title>{`${t.profile.documentTitle} · StackOff`}</title>
      {profile.data ? (
        <ProfileView me={profile.data[0]} map={profile.data[1]} history={profile.data[2]} stats={profile.data[3]} />
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

interface ProfileViewProps {
  me: MeDto
  map: KnowledgeMapDto
  history: MatchSummaryDto[]
  stats: StatsDto
}

function ProfileView({ me, map, history, stats }: ProfileViewProps) {
  // The fresh MeDto goes into the session, and the card reads it from there, so equipping in the locker shows at once.
  const { session, setUser } = useAuth()
  useEffect(() => setUser(me), [me, setUser])
  const current = session.status === 'authenticated' ? session.user : me

  return (
    <div className={GRID}>
      <ProfileCard me={current} stats={stats} className="xl:col-span-2" />
      <SkillGraphCard topics={map.topics} className="xl:col-span-2 xl:row-start-2" />
      <MatchHistoryCard history={history} className="xl:col-start-3 xl:row-span-2 xl:row-start-1" />
      <LockerCard className="xl:col-span-3" />
      <KnowledgeMapCard map={map} className="xl:col-span-3" />
      <StatsCard stats={stats} className="xl:col-span-3" />
      <HonorsCard className="xl:col-span-3" />
    </div>
  )
}

function ProfileCard({ me, stats: totals, className }: { me: MeDto; stats: StatsDto; className?: string }) {
  const { t, locale } = useLocale()
  const copy = t.profile
  const [avatarOpen, setAvatarOpen] = useState(false)
  const stats: [string, string | number][] = [
    [copy.stats.duels, totals.matches],
    [copy.stats.wins, totals.wins],
    [copy.stats.winRate, totals.matches ? `${Math.round(totals.winRate)}%` : '–'],
    [copy.stats.streak, me.streak.count],
  ]

  return (
    <Card className={cn(className, cardClass(me))}>
      <CardContent className="grid gap-6">
        {bannerClass(me) && <div aria-hidden="true" className={cn('-mb-2 h-14 rounded-lg', bannerClass(me))} />}
        <div className="flex flex-wrap items-center gap-4">
          {/* The portrait is the way in: a tap opens the 30 to pick from, and the pencil says it can change. */}
          <button
            type="button"
            onClick={() => setAvatarOpen(true)}
            className="group/avatar relative shrink-0 rounded-full focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
          >
            <span className="sr-only">{t.avatar.change}</span>
            <UserAvatar user={me} className="size-16 transition-opacity group-hover/avatar:opacity-80 motion-reduce:transition-none" />
            <span
              aria-hidden="true"
              className="absolute -right-1 -bottom-1 grid size-7 place-items-center rounded-full bg-secondary text-foreground ring-2 ring-card transition-colors group-hover/avatar:bg-primary group-hover/avatar:text-primary-foreground motion-reduce:transition-none"
            >
              <Pencil className="size-3.5" />
            </span>
          </button>
          <AvatarDialog user={me} open={avatarOpen} onOpenChange={setAvatarOpen} />
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
        <Visibility />
      </CardContent>
    </Card>
  )
}

// PATCH /api/me { profileVisibility }: friends-only answers 403 to everyone else (docs §6.3).
function Visibility() {
  const { t } = useLocale()
  const copy = t.profile.visibility
  const { session, updateMe } = useAuth()
  const [error, setError] = useState<string>()
  if (session.status !== 'authenticated') return null

  async function change(profileVisibility: ProfileVisibility) {
    setError(undefined)
    await updateMe({ profileVisibility }).catch((caught: Error) => setError(caught.message))
  }

  return (
    // Laid out like the mode switch on Practice: a mono label, the sliding two-way toggle, then what it means.
    <div className="flex flex-wrap items-center gap-x-4 gap-y-2">
      <span aria-hidden="true" className="font-mono text-xs tracking-wide text-muted-foreground uppercase">
        {copy.label}
      </span>
      <SegmentedControl
        label={copy.label}
        name="profile-visibility"
        value={session.user.profileVisibility}
        onChange={change}
        toggle
        options={[
          { value: 'public', label: copy.public },
          { value: 'friends', label: copy.friends },
        ]}
      />
      <p aria-live="polite" className="text-xs text-pretty text-muted-foreground">
        {copy.hint[session.user.profileVisibility]}
      </p>
      {error && (
        <p role="alert" className="basis-full text-sm text-destructive">
          {error}
        </p>
      )}
    </div>
  )
}
