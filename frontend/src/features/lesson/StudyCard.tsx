import { CircleAlert } from 'lucide-react'
import type { ReactNode } from 'react'
import { Card, CardContent } from '@/components/ui/card'
import { CodeBlock } from '@/features/match/CodeBlock'
import { useLocale } from '@/lib/i18n/locale-context'

interface StudyCardProps {
  meta: ReactNode // "Card 2 of 4" and the concept or topic
  title: string
  body: ReactNode
  code?: string | null
  language?: string | null
  misconception?: string | null
}

// One lesson card: the idea, why it matters, an example, the common mistake. Shared by duel and professor lessons.
export function StudyCard({ meta, title, body, code, language, misconception }: StudyCardProps) {
  const { t } = useLocale()

  return (
    <Card>
      <CardContent className="grid gap-4">
        <p className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">{meta}</p>
        <h2 className="font-heading text-xl font-semibold text-balance sm:text-2xl">{title}</h2>
        <p className="text-pretty">{body}</p>
        {code && <CodeBlock code={code} language={language} />}
        {misconception && (
          <div className="grid gap-1 rounded-lg bg-warning/10 p-3 ring-1 ring-warning/40 ring-inset">
            <p className="flex items-center gap-1.5 text-xs font-medium text-warning">
              <CircleAlert aria-hidden="true" className="size-3.5" />
              {t.lesson.misconception}
            </p>
            <p className="text-sm text-pretty">{misconception}</p>
          </div>
        )}
      </CardContent>
    </Card>
  )
}
