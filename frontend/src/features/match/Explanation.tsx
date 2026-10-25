import { Lightbulb, LoaderCircle } from 'lucide-react'
import { useCallback } from 'react'
import { useAsync } from '@/hooks/use-async'
import { api } from '@/lib/api-client'
import { useLocale } from '@/lib/i18n/locale-context'
import type { ExplanationDto } from '@/types/api'

interface ExplanationProps {
  questionId: string
  initial?: ExplanationDto | null // the review already carries it once someone has opened it
  showUnavailable?: boolean // say so when the AI is down, instead of showing nothing
}

// The AI explanation of an answered question. Its first request can take a few seconds; when the AI is unavailable
// (source 'static') only the "remember" line next to it carries the lesson.
export function Explanation({ questionId, initial, showUnavailable }: ExplanationProps) {
  const { t } = useLocale()
  const load = useCallback(
    (signal: AbortSignal) => (initial ? Promise.resolve(initial) : api.explanation(questionId, signal)),
    [questionId, initial],
  )
  const { data, loading, error } = useAsync(load)

  if (loading)
    return (
      <p role="status" className="flex items-center gap-2 text-sm text-muted-foreground">
        <LoaderCircle aria-hidden="true" className="size-4 animate-spin motion-reduce:animate-none" />
        {t.match.explaining}
      </p>
    )
  if (error || data?.source !== 'ai')
    return showUnavailable ? <p className="text-sm text-muted-foreground">{t.match.explanationUnavailable}</p> : null
  return (
    <div className="grid gap-2 text-sm text-pretty">
      {data.whyCorrect && (
        <p>
          <span className="font-medium text-success">{t.match.whyCorrect}:</span> {data.whyCorrect}
        </p>
      )}
      {data.whyDistractor && (
        <p>
          <span className="font-medium text-destructive">{t.match.whyDistractor}:</span> {data.whyDistractor}
        </p>
      )}
    </div>
  )
}

// The line to remember after a question: a calm tile, sky only in its label.
export function Remember({ text }: { text: string }) {
  const { t } = useLocale()
  return (
    <div className="grid gap-1 rounded-lg bg-secondary px-4 py-3">
      <p className="flex items-center gap-1.5 font-mono text-xs font-medium tracking-wide text-primary uppercase">
        <Lightbulb aria-hidden="true" className="size-3.5" />
        {t.match.remember}
      </p>
      <p className="text-base leading-snug text-pretty">{text}</p>
    </div>
  )
}
