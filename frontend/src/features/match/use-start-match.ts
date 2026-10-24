import { useState } from 'react'
import { useNavigate } from 'react-router'
import { api, ApiError } from '@/lib/api-client'
import { useLocale } from '@/lib/i18n/locale-context'
import type { MatchStateDto } from '@/types/api'

// Starts a match (bot, practice, calibration) and opens it. When one is already running
// (409 already-in-match, another tab or a reload mid-duel) it opens that one instead of failing.
export function useStartMatch() {
  const navigate = useNavigate()
  const { t } = useLocale()
  const [starting, setStarting] = useState(false)
  const [error, setError] = useState<string>()

  async function start(run: () => Promise<MatchStateDto>) {
    if (starting) return
    setStarting(true)
    setError(undefined)
    try {
      navigate(`/match/${(await run()).id}`)
    } catch (caught) {
      if (caught instanceof ApiError && caught.code === 'urn:app:match/already-in-match') {
        const me = await api.me().catch(() => undefined)
        if (me?.activeMatchId) return navigate(`/match/${me.activeMatchId}`)
      }
      setError(caught instanceof ApiError && caught.status === 0 ? t.common.serverUnreachable : String((caught as Error).message))
      setStarting(false)
    }
  }

  return { start, starting, error }
}
