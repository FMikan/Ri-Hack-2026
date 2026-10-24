import { RotateCcw, Swords } from 'lucide-react'
import { useState } from 'react'
import { UserAvatar } from '@/components/shared/UserAvatar'
import { Button } from '@/components/ui/button'
import { useOpenMatch, useStartMatch } from '@/features/match/use-start-match'
import { api, ApiError } from '@/lib/api-client'
import { useLocale } from '@/lib/i18n/locale-context'
import type { DashboardDto } from '@/types/api'

type RematchInvite = NonNullable<DashboardDto['inbox']['rematch']>
type ChallengeInvite = NonNullable<DashboardDto['inbox']['friendChallenge']>

// Requests waiting for me (game idea §4, §13): a rematch the last opponent asked for and a friend's challenge. The
// dashboard polls, so they show up within a few seconds; accepting opens the new duel.
export function Inbox({ inbox }: { inbox: DashboardDto['inbox'] }) {
  return (
    <>
      {inbox.rematch && <RematchInviteCard key={inbox.rematch.matchId} invite={inbox.rematch} />}
      {inbox.friendChallenge && <ChallengeInviteCard key={inbox.friendChallenge.code} invite={inbox.friendChallenge} />}
    </>
  )
}

function ChallengeInviteCard({ invite }: { invite: ChallengeInvite }) {
  const { t } = useLocale()
  const { start, starting, error } = useStartMatch((caught) =>
    caught.code === 'urn:app:challenge/expired' ? t.friends.join.expired : caught.code === 'urn:app:topics/count' ? t.friends.join.needTopics : undefined,
  )

  return (
    <section aria-live="polite" className="flex flex-wrap items-center gap-3 rounded-xl bg-primary/10 p-4 ring-1 ring-primary/40 ring-inset">
      <UserAvatar user={invite.host} className="size-10" />
      <p className="min-w-0 flex-1 font-medium">{t.friends.join.invite(invite.host.username)}</p>
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
      <Button aria-disabled={starting} onClick={() => start(() => api.joinChallenge(invite.code))}>
        <Swords aria-hidden="true" />
        {t.friends.join.accept}
      </Button>
    </section>
  )
}

function RematchInviteCard({ invite }: { invite: RematchInvite }) {
  const { t } = useLocale()
  const openMatch = useOpenMatch()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string>()

  async function accept() {
    setBusy(true)
    setError(undefined)
    try {
      openMatch((await api.acceptRematch(invite.matchId)).id)
    } catch (caught) {
      setError(
        caught instanceof ApiError && caught.code === 'urn:app:match/rematch-unavailable'
          ? t.match.rematch.expired
          : caught instanceof ApiError && caught.status === 0
            ? t.common.serverUnreachable
            : String((caught as Error).message),
      )
      setBusy(false)
    }
  }

  return (
    <section aria-live="polite" className="flex flex-wrap items-center gap-3 rounded-xl bg-primary/10 p-4 ring-1 ring-primary/40 ring-inset">
      <UserAvatar user={invite.from} className="size-10" />
      <p className="min-w-0 flex-1 font-medium">{t.match.rematch.asked(invite.from.username)}</p>
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
      <Button aria-disabled={busy} onClick={() => !busy && accept()}>
        <RotateCcw aria-hidden="true" />
        {t.match.rematch.accept}
      </Button>
    </section>
  )
}
