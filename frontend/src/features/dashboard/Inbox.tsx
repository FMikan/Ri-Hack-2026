import { RotateCcw, Swords, X } from 'lucide-react'
import { useState } from 'react'
import { UserAvatar } from '@/components/shared/UserAvatar'
import { Button } from '@/components/ui/button'
import { useOpenMatch, useStartMatch } from '@/features/match/use-start-match'
import { api, ApiError } from '@/lib/api-client'
import { useLocale } from '@/lib/i18n/locale-context'
import type { InboxDto } from '@/types/api'

type RematchInvite = NonNullable<InboxDto['rematch']>
type ChallengeInvite = NonNullable<InboxDto['friendChallenge']>

// Requests waiting for me (game idea §4, §13): a rematch the last opponent asked for and a friend's challenge. The
// dashboard polls, so they show up within a few seconds and leave once they expire; accepting opens the new duel.
export function Inbox({ inbox, onChanged }: { inbox: InboxDto; onChanged: () => void }) {
  return (
    <>
      {inbox.rematch && <RematchInviteCard key={inbox.rematch.matchId} invite={inbox.rematch} />}
      {inbox.friendChallenge && <ChallengeInviteCard key={inbox.friendChallenge.code} invite={inbox.friendChallenge} onDeclined={onChanged} />}
    </>
  )
}

function ChallengeInviteCard({ invite, onDeclined }: { invite: ChallengeInvite; onDeclined: () => void }) {
  const { t } = useLocale()
  const { start, starting, error } = useStartMatch((caught) =>
    caught.code === 'urn:app:challenge/expired' ? t.friends.join.expired : caught.code === 'urn:app:topics/count' ? t.friends.join.needTopics : undefined,
  )
  const [declined, setDeclined] = useState(false)

  // Hidden at once; if the request fails the card comes back, so the challenge is never silently left open.
  async function decline() {
    setDeclined(true)
    try {
      await api.declineChallenge(invite.code)
      onDeclined()
    } catch {
      setDeclined(false)
    }
  }

  if (declined) return null

  return (
    <section aria-live="polite" className="flex flex-wrap items-center gap-3 rounded-xl bg-primary/10 p-4 ring-1 ring-primary/40 ring-inset">
      <UserAvatar user={invite.host} className="size-10" />
      <p className="min-w-0 flex-1 font-medium">{t.friends.join.invite(invite.host.username)}</p>
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
      <Button variant="ghost" aria-disabled={starting} onClick={() => !starting && decline()} className="text-muted-foreground">
        <X aria-hidden="true" />
        {t.friends.join.decline}
      </Button>
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
