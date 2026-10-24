import { LoaderCircle, RotateCcw } from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'
import { Button } from '@/components/ui/button'
import { usePoll } from '@/hooks/use-poll'
import { api, ApiError } from '@/lib/api-client'
import { useLocale } from '@/lib/i18n/locale-context'
import type { MatchStateDto, RematchDto } from '@/types/api'
import { useOpenMatch } from './use-start-match'

const isSettled = (rematch: RematchDto) => rematch.status !== 'none' && rematch.status !== 'pending'

// After a ranked or friend duel with a human who is still around: either player asks, the other has 20 s to accept,
// and both land in the new duel (it starts with its own veto). Polls every 2 s so the other side's request shows up.
export function RematchPanel({ match, opponentName }: { match: MatchStateDto; opponentName: string }) {
  const { t } = useLocale()
  const copy = t.match.rematch
  const openMatch = useOpenMatch()
  const load = useCallback((signal: AbortSignal) => api.rematch(match.id, signal), [match.id])
  const { data: rematch, replace } = usePoll(load, 2000, isSettled)
  const [busy, setBusy] = useState(false)
  const [expired, setExpired] = useState(false)
  const pending = rematch?.status === 'pending'
  const newMatchId = rematch?.status === 'accepted' ? rematch.matchId : undefined

  useEffect(() => {
    if (newMatchId) openMatch(newMatchId)
  }, [newMatchId, openMatch])

  async function send(run: () => Promise<RematchDto | MatchStateDto>) {
    setBusy(true)
    try {
      const response = await run()
      if ('players' in response) openMatch(response.id)
      else replace(response)
    } catch (caught) {
      if (caught instanceof ApiError && caught.code === 'urn:app:match/rematch-unavailable') setExpired(true)
    } finally {
      setBusy(false)
    }
  }

  if (!rematch) return null
  if (expired || rematch.status === 'expired' || rematch.status === 'declined')
    return <p className="text-center text-sm text-muted-foreground">{copy.expired}</p>
  if (rematch.status === 'accepted')
    return (
      <p role="status" className="flex items-center justify-center gap-2 text-sm text-muted-foreground">
        <LoaderCircle aria-hidden="true" className="size-4 animate-spin motion-reduce:animate-none" />
        {copy.opening}
      </p>
    )

  // RematchDto carries no server time, so the 20 s window is stated in the copy instead of counted down.
  const theyAsked = pending && rematch.requestedBySeat !== match.mySeat

  return (
    <div aria-live="polite" className="flex flex-wrap items-center justify-center gap-3 rounded-xl bg-card p-4 ring-1 ring-foreground/10">
      {pending && !theyAsked ? (
        <p role="status" className="flex items-center gap-2 text-sm text-muted-foreground">
          <LoaderCircle aria-hidden="true" className="size-4 animate-spin motion-reduce:animate-none" />
          {copy.waiting(opponentName)}
        </p>
      ) : (
        <>
          {theyAsked && <p className="text-sm font-medium">{copy.asked(opponentName)}</p>}
          <Button
            aria-disabled={busy}
            onClick={() => !busy && send(theyAsked ? () => api.acceptRematch(match.id) : () => api.requestRematch(match.id))}
          >
            <RotateCcw aria-hidden="true" />
            {theyAsked ? copy.accept : copy.ask}
          </Button>
        </>
      )}
    </div>
  )
}
