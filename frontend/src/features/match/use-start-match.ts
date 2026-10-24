import { useCallback, useState } from 'react'
import { useNavigate } from 'react-router'
import { api, ApiError } from '@/lib/api-client'
import { useLocale } from '@/lib/i18n/locale-context'
import type { MatchStateDto } from '@/types/api'

// Opens a match. The play dialog's ?play= entry is dropped first, so Back from the duel does not reopen the dialog
// (for ranked that would join the queue again).
export function useOpenMatch() {
  const navigate = useNavigate()
  return useCallback(
    (id: string) => {
      if (new URLSearchParams(window.location.search).has('play')) navigate({ search: '' }, { replace: true })
      navigate(`/match/${id}`)
    },
    [navigate],
  )
}

// Starts a match (bot, practice, calibration, ghost) and opens it. When one is already running
// (409 already-in-match, another tab or a reload mid-duel) it opens that one instead of failing.
export function useStartMatch() {
  const openMatch = useOpenMatch()
  const { t } = useLocale()
  const [starting, setStarting] = useState(false)
  const [error, setError] = useState<string>()

  async function start(run: () => Promise<MatchStateDto>) {
    if (starting) return
    setStarting(true)
    setError(undefined)
    try {
      openMatch((await run()).id)
    } catch (caught) {
      if (caught instanceof ApiError && caught.code === 'urn:app:match/already-in-match') {
        const me = await api.me().catch(() => undefined)
        if (me?.activeMatchId) return openMatch(me.activeMatchId)
      }
      setError(caught instanceof ApiError && caught.status === 0 ? t.common.serverUnreachable : String((caught as Error).message))
      setStarting(false)
    }
  }

  return { start, starting, error }
}
