import { Award, Lock, UserPlus } from 'lucide-react'
import { createElement, useCallback, useState } from 'react'
import { Navigate, useParams } from 'react-router'
import { Meter } from '@/components/shared/Meter'
import { UserAvatar } from '@/components/shared/UserAvatar'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useAuth } from '@/features/auth/auth-context'
import { useAsync } from '@/hooks/use-async'
import { api, ApiError } from '@/lib/api-client'
import { bannerClass, cardClass, nameClass } from '@/lib/cosmetics'
import { useLocale } from '@/lib/i18n/locale-context'
import { topicIcon } from '@/lib/topic-icons'
import { cn } from '@/lib/utils'
import type { PublicProfileDto } from '@/types/api'

// Another player's profile (docs §6.3): topic-level progress, titles, achievements and three stats. Concept mastery
// stays private, and a friends-only profile answers 403 to everyone else.
export function PublicProfilePage() {
  const { username = '' } = useParams()
  const { t } = useLocale()
  const copy = t.publicProfile
  const { session } = useAuth()
  const load = useCallback((signal: AbortSignal) => api.publicProfile(username, signal), [username])
  const profile = useAsync(load)

  if (session.status === 'authenticated' && session.user.username.toLowerCase() === username.toLowerCase())
    return <Navigate to="/profile" replace />

  const error = profile.error instanceof ApiError ? profile.error : undefined
  return (
    <main className="mx-auto grid w-full max-w-5xl content-start gap-5 px-4 pb-10 sm:px-6 lg:px-8">
      <title>{`${username} · StackOff`}</title>
      {profile.data ? (
        <ProfileView profile={profile.data} />
      ) : error?.code === 'urn:app:users/profile-private' ? (
        <Card>
          <CardContent className="grid justify-items-start gap-3">
            <h1 className="flex items-center gap-2 font-heading text-2xl font-semibold">
              <Lock aria-hidden="true" className="size-5 text-muted-foreground" />
              {username}
            </h1>
            <p className="text-pretty text-muted-foreground">{copy.private}</p>
            <AddFriend username={username} />
          </CardContent>
        </Card>
      ) : profile.error ? (
        <Card role="alert">
          <CardContent className="grid justify-items-start gap-3">
            <p>{error?.status === 404 ? copy.notFound(username) : copy.loadFailed}</p>
            {error?.status !== 404 && (
              <Button variant="outline" onClick={profile.reload}>
                {t.common.retry}
              </Button>
            )}
          </CardContent>
        </Card>
      ) : (
        <div aria-busy="true" className="grid gap-4">
          <p role="status" className="sr-only">
            {t.common.loading}
          </p>
          <Skeleton className="h-52 rounded-xl" />
          <Skeleton className="h-72 rounded-xl" />
        </div>
      )}
    </main>
  )
}

function ProfileView({ profile }: { profile: PublicProfileDto }) {
  const { t } = useLocale()
  const copy = t.publicProfile
  const { user, stats } = profile
  const tiles: [string, string | number][] = [
    [copy.stats.matches, stats.matches],
    [copy.stats.winRate, `${Math.round(stats.winRate)}%`],
    [copy.stats.graduated, stats.conceptsGraduated],
  ]

  return (
    <>
      <Card className={cardClass(user)}>
        <CardContent className="grid gap-6">
          {bannerClass(user) && <div aria-hidden="true" className={cn('-mb-2 h-14 rounded-lg', bannerClass(user))} />}
          <div className="flex flex-wrap items-center gap-4">
            <UserAvatar user={user} className="size-16" />
            <div className="grid min-w-0 gap-1">
              <h1 className={cn('truncate font-heading text-2xl font-semibold tracking-tight sm:text-3xl', nameClass(user))}>{user.username}</h1>
              {user.title && <p className="text-sm font-medium text-primary">{user.title}</p>}
            </div>
            <div className="flex flex-wrap items-center gap-2 sm:ml-auto">
              <Badge variant="secondary">{t.dashboard.tiers[user.tier]}</Badge>
              <Badge variant="secondary">{user.isProvisional ? copy.placement : t.profile.rating(user.rating)}</Badge>
            </div>
          </div>
          <dl className="grid grid-cols-3 gap-3">
            {tiles.map(([label, value]) => (
              <div key={label} className="flex flex-col-reverse gap-1 rounded-lg bg-secondary p-4">
                <dt className="text-sm text-muted-foreground">{label}</dt>
                <dd className="font-heading text-2xl font-semibold tabular-nums sm:text-3xl">{value}</dd>
              </div>
            ))}
          </dl>
          <AddFriend username={user.username} />
        </CardContent>
      </Card>

      <div className="grid items-start gap-5 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>
              <h2>{copy.topics}</h2>
            </CardTitle>
          </CardHeader>
          <CardContent>
            {profile.topics.length ? (
              <ul className="grid gap-3">
                {profile.topics.map(({ topic, mastery, completion }) => (
                  <li key={topic.id} className="grid gap-1.5">
                    <p className="flex items-center gap-2 text-sm">
                      {createElement(topicIcon(topic.slug), { 'aria-hidden': true, className: 'size-4 shrink-0 text-primary' })}
                      <span className="min-w-0 flex-1 truncate font-medium">{topic.name}</span>
                      <span className="font-mono text-xs text-muted-foreground">
                        {mastery == null ? t.practice.topics.notPlayed : `${Math.round(mastery)}%`} · {t.profile.map.completion(Math.round(completion))}
                      </span>
                    </p>
                    <Meter value={(mastery ?? 0) / 100} label={topic.name} />
                  </li>
                ))}
              </ul>
            ) : (
              <p className="text-sm text-muted-foreground">{copy.noTopics}</p>
            )}
          </CardContent>
        </Card>
        <Card>
          <CardHeader>
            <CardTitle>
              <h2>{copy.honors}</h2>
            </CardTitle>
          </CardHeader>
          <CardContent className="grid gap-4">
            {profile.titles.length > 0 && (
              <ul className="flex flex-wrap gap-2">
                {profile.titles.map((title) => (
                  <li key={title}>
                    <Badge variant="outline">{title}</Badge>
                  </li>
                ))}
              </ul>
            )}
            {profile.achievements.length ? (
              <ul className="grid gap-2 sm:grid-cols-2">
                {profile.achievements.map((key) => (
                  <li key={key} className="flex items-center gap-2 rounded-lg bg-secondary px-3 py-2.5 text-sm">
                    <Award aria-hidden="true" className="size-4 shrink-0 text-warning" />
                    {t.achievements[key as keyof typeof t.achievements] ?? key}
                  </li>
                ))}
              </ul>
            ) : (
              profile.titles.length === 0 && <p className="text-sm text-muted-foreground">{copy.noHonors}</p>
            )}
          </CardContent>
        </Card>
      </div>
    </>
  )
}

function AddFriend({ username }: { username: string }) {
  const { t } = useLocale()
  const copy = t.publicProfile
  const [state, setState] = useState<{ text: string; ok: boolean }>()
  const [busy, setBusy] = useState(false)

  async function add() {
    setBusy(true)
    try {
      const friend = await api.addFriend({ username })
      setState({ text: friend.status === 'accepted' ? t.friends.add.accepted(username) : t.friends.add.sent(username), ok: true })
    } catch (caught) {
      setState({
        text: caught instanceof ApiError && caught.code === 'urn:app:friends/already' ? t.friends.errors.already : t.auth.errors.generic,
        ok: false,
      })
    } finally {
      setBusy(false)
    }
  }

  if (state)
    return (
      <p role="status" className={cn('text-sm', state.ok ? 'text-success' : 'text-muted-foreground')}>
        {state.text}
      </p>
    )
  return (
    <Button variant="outline" className="justify-self-start" aria-disabled={busy} onClick={() => !busy && add()}>
      <UserPlus aria-hidden="true" />
      {copy.addFriend}
    </Button>
  )
}
