import { Swords } from 'lucide-react'
import { useCallback } from 'react'
import { Link, useParams } from 'react-router'
import { UserAvatar } from '@/components/shared/UserAvatar'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useAuth } from '@/features/auth/auth-context'
import { useStartMatch } from '@/features/match/use-start-match'
import { useAsync } from '@/hooks/use-async'
import { api, ApiError } from '@/lib/api-client'
import { nameClass } from '@/lib/cosmetics'
import { ratingText } from '@/lib/game'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import { ChallengeWaiting } from './ChallengeWaiting'

// The link a friend shares: /challenge/ABC234. A friendly duel plays like ranked (veto, 10 questions) with no rating.
export function ChallengePage() {
  const { code = '' } = useParams()
  const { t } = useLocale()
  const copy = t.friends.join
  const { session } = useAuth()
  const load = useCallback((signal: AbortSignal) => api.challenge(code, signal), [code])
  const challenge = useAsync(load)
  // match/already-in-match never shows: useStartMatch opens the running duel instead.
  const { start, starting, error } = useStartMatch((caught) =>
    caught.code === 'urn:app:challenge/expired' ? copy.expired : caught.code === 'urn:app:topics/count' ? copy.needTopics : undefined,
  )
  const meId = session.status === 'authenticated' ? session.user.id : undefined
  const data = challenge.data

  return (
    <main className="mx-auto grid w-full max-w-xl content-start gap-5 px-4 pb-10 sm:px-6">
      <title>{`${copy.documentTitle} · StackOff`}</title>
      <h1 className="font-heading text-2xl font-semibold tracking-tight sm:text-3xl">{copy.title}</h1>
      {data ? (
        <Card>
          <CardContent className="grid justify-items-center gap-4 py-4 text-center">
            {data.host.id === meId ? (
              // My own link: keep waiting for someone to join.
              <ChallengeWaiting code={data.code} expiresAt={data.expiresAt} />
            ) : data.matchId ? (
              <p className="text-pretty text-muted-foreground">{copy.taken}</p>
            ) : (
              <>
                <UserAvatar user={data.host} className="size-20" />
                <div className="grid gap-0.5">
                  <p className={cn('font-heading text-xl font-semibold', nameClass(data.host))}>{data.host.username}</p>
                  <p className="text-sm text-muted-foreground">
                    {data.host.title ?? t.dashboard.tiers[data.host.tier]} · {ratingText(data.host.rating, data.host.isProvisional)}
                  </p>
                </div>
                <p className="max-w-sm text-pretty">{copy.body}</p>
                {error && (
                  <p role="alert" className="text-sm text-destructive">
                    {error}
                  </p>
                )}
                <Button size="lg" aria-disabled={starting} onClick={() => start(() => api.joinChallenge(code))}>
                  <Swords aria-hidden="true" />
                  {copy.accept}
                </Button>
              </>
            )}
          </CardContent>
        </Card>
      ) : challenge.error ? (
        <Card role="alert">
          <CardContent className="grid justify-items-start gap-3">
            <p>{challenge.error instanceof ApiError && challenge.error.status === 404 ? copy.notFound : copy.loadFailed}</p>
            <Button asChild variant="outline">
              <Link to="/friends">{t.shell.nav.friends}</Link>
            </Button>
          </CardContent>
        </Card>
      ) : (
        <Skeleton aria-busy="true" className="h-72 rounded-xl" />
      )}
    </main>
  )
}
