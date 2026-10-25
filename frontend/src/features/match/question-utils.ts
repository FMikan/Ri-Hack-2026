import type { Messages } from '@/lib/i18n/en'
import type { QuestionContent } from '@/types/api'

export type Mark = 'idle' | 'picked' | 'correct' | 'wrong' | 'dimmed'

// Before the key is known the pick is just "picked"; in the interstitial the key turns it green or red.
export function markOf(id: string, picked: string | undefined, correct: string | undefined): Mark {
  if (correct === undefined) return id === picked ? 'picked' : 'idle'
  if (id === correct) return 'correct'
  return id === picked ? 'wrong' : 'dimmed'
}

// A hairline frames a tile; 2px and a color only carry state (mine, right, wrong). Sky is never spent on idle tiles.
export const markStyles: Record<Mark, string> = {
  idle: 'bg-secondary ring-1 ring-border',
  picked: 'bg-primary/15 ring-2 ring-primary',
  correct: 'bg-success/15 text-success ring-2 ring-success',
  wrong: 'bg-destructive/15 text-destructive ring-2 ring-destructive',
  dimmed: 'bg-secondary text-muted-foreground ring-1 ring-border',
}

// The letter, number or true/false badge carries the same state as its tile; filled ones take the dark text.
export const badgeStyles: Record<Mark, string> = {
  idle: 'bg-primary/10 text-primary',
  picked: 'bg-primary text-primary-foreground',
  correct: 'bg-success text-primary-foreground',
  wrong: 'bg-destructive text-primary-foreground',
  dimmed: 'bg-background/60 text-muted-foreground',
}

export const badge = 'grid size-7 shrink-0 place-items-center rounded-md font-mono text-xs transition-colors duration-150 motion-reduce:transition-none'
// Hover only lifts an idle tile's hairline; the other states keep their ring.
export const idleHover = 'enabled:hover:ring-input'

export function promptOf(content: QuestionContent, t: Messages) {
  if (content.type === 'true_false') return content.statement
  if (content.type === 'cloze') return content.prompt ?? t.match.prompts.cloze
  if (content.type === 'output') return content.prompt ?? t.match.prompts.output
  return content.prompt
}
