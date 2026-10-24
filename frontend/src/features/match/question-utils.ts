import type { Messages } from '@/lib/i18n/en'
import type { QuestionContent } from '@/types/api'

export type Mark = 'idle' | 'picked' | 'correct' | 'wrong' | 'dimmed'

// Before the key is known the pick is just "picked"; in the interstitial the key turns it green or red.
export function markOf(id: string, picked: string | undefined, correct: string | undefined): Mark {
  if (correct === undefined) return id === picked ? 'picked' : 'idle'
  if (id === correct) return 'correct'
  return id === picked ? 'wrong' : 'dimmed'
}

export const markStyles: Record<Mark, string> = {
  idle: 'bg-secondary ring-border',
  picked: 'bg-primary/15 text-primary ring-primary',
  correct: 'bg-success/15 text-success ring-success',
  wrong: 'bg-destructive/15 text-destructive ring-destructive',
  dimmed: 'bg-secondary text-muted-foreground ring-border',
}

export function promptOf(content: QuestionContent, t: Messages) {
  if (content.type === 'true_false') return content.statement
  if (content.type === 'cloze') return content.prompt ?? t.match.prompts.cloze
  if (content.type === 'output') return content.prompt ?? t.match.prompts.output
  return content.prompt
}
