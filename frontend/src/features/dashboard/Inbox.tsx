import { RotateCcw } from 'lucide-react'
import { useState } from 'react'
import { UserAvatar } from '@/components/shared/UserAvatar'
import { Button } from '@/components/ui/button'
import { useOpenMatch } from '@/features/match/use-start-match'
import { api, ApiError } from '@/lib/api-client'
import { useLocale } from '@/lib/i18n/locale-context'
import type { DashboardDto } from '@/types/api'

type RematchInvite = NonNullable<DashboardDto['inbox']['rematch']>

// Requests waiting for me (game idea §4, §13): a rematch the last opponent asked for. The dashboard polls, so it shows
// up within a few seconds; accepting opens the new duel.
export function Inbox({ inbox }: { inbox: DashboardDto['inbox'] }) {
  if (!inbox.rematch) return null
  return <RematchInviteCard key={inbox.rematch.matchId} invite={inbox.rematch} />
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
