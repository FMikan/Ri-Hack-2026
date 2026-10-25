import { useEffect, useRef, useState } from 'react'
import { useNavigate } from 'react-router'
import { api } from '@/lib/api-client'
import { useLocale } from '@/lib/i18n/locale-context'
import { professorError } from './professor-error'

export type StudyKind = 'lesson' | 'quiz'

export interface Making {
  kind: StudyKind
  messageId: string
  topic: string
  startedAt: number // performance.now(), for the elapsed seconds
}

// Asks the professor for the lesson or quiz of one answer (10-30 s) and opens it. An answer that already has one gets
// it back at once. Leaving the page meanwhile lets the request finish without dragging the player to the result.
export function useCreateStudy() {
  const navigate = useNavigate()
  const { t } = useLocale()
  const [making, setMaking] = useState<Making>()
  const [error, setError] = useState<{ messageId: string; text: string }>()
  const mounted = useRef(true)
  useEffect(() => {
    mounted.current = true
    return () => {
      mounted.current = false
    }
  }, [])

  async function make(kind: StudyKind, messageId: string, topic: string) {
    if (making) return
    setMaking({ kind, messageId, topic, startedAt: performance.now() })
    setError(undefined)
    try {
      const path =
        kind === 'lesson'
          ? `/professor/lessons/${(await api.createProfessorLesson(messageId)).id}`
          : `/professor/quizzes/${(await api.createProfessorQuiz(messageId)).id}`
      if (mounted.current) navigate(path)
    } catch (caught) {
      if (!mounted.current) return
      setError({ messageId, text: professorError(caught, t) })
      setMaking(undefined)
    }
  }

  return { make, making, error }
}
