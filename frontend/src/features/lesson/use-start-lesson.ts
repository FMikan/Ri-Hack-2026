import { useState } from 'react'
import { useNavigate } from 'react-router'
import { api, ApiError } from '@/lib/api-client'
import { useLocale } from '@/lib/i18n/locale-context'

// Opens the lesson for a finished match: the existing one when the match has one (lessonId), otherwise a new one.
// A second lesson for the same match pays no reward, so reopening the first matters (docs §6.11 F2).
export function useStartLesson(matchId: string, lessonId?: string | null) {
  const navigate = useNavigate()
  const { t } = useLocale()
  const [starting, setStarting] = useState(false)
  const [error, setError] = useState<string>()

  async function start() {
    if (lessonId) return navigate(`/lesson/${lessonId}`)
    if (starting) return
    setStarting(true)
    setError(undefined)
    try {
      navigate(`/lesson/${(await api.startLesson(matchId)).id}`)
    } catch (caught) {
      setError(
        caught instanceof ApiError && caught.code === 'urn:app:lesson/nothing-missed'
          ? t.lesson.nothingMissed
          : caught instanceof ApiError && caught.status === 0
            ? t.common.serverUnreachable
            : String((caught as Error).message),
      )
      setStarting(false)
    }
  }

  return { start, starting, error }
}
