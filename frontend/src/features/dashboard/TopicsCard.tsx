import { Check, Pencil } from 'lucide-react'
import { useState } from 'react'
import { Button } from '@/components/ui/button'
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { api, ApiError } from '@/lib/api-client'
import { MAX_TOPICS, MIN_TOPICS } from '@/lib/game'
import type { Messages } from '@/lib/i18n/en'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import type { DashboardTopicDto } from '@/types/api'
import { topicIcon } from '@/lib/topic-icons'

interface TopicsCardProps {
  topics: DashboardTopicDto[]
  queueLocked: boolean
  onSaved: () => void
  id?: string
  className?: string
}

// One line per topic, icon then name; tall enough for a 44px touch target.
const row = 'flex h-11 w-full min-w-0 items-center gap-3 rounded-lg px-3 text-left text-sm font-medium ring-1 ring-inset'

function saveError(error: unknown, t: Messages) {
  if (!(error instanceof ApiError)) return t.auth.errors.generic
  if (error.code === 'urn:app:topics/count') return t.dashboard.topics.countError
  if (error.code === 'urn:app:topics/locked-in-queue') return t.dashboard.topics.locked
  return error.status === 0 ? t.common.serverUnreachable : t.auth.errors.generic
}

// Every topic in a list that scrolls inside the card, active ones first and in color. With fewer than 3 active
// topics the card is the onboarding step and stays in edit mode.
export function TopicsCard({ topics, queueLocked, onSaved, id, className }: TopicsCardProps) {
  const { t } = useLocale()
  const copy = t.dashboard.topics
  const activeIds = topics.filter((entry) => entry.active).map((entry) => entry.topic.id)
  const onboarding = activeIds.length < MIN_TOPICS
  const [editing, setEditing] = useState(false)
  const [selected, setSelected] = useState(() => new Set(activeIds))
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string>()
  const isEditing = editing || onboarding
  // Sorted by the saved state, so rows do not jump while toggling.
  const ordered = [...topics].sort((a, b) => Number(b.active) - Number(a.active))

  function toggle(id: string) {
    setError(undefined)
    setSelected((previous) => {
      const next = new Set(previous)
      if (!next.delete(id)) next.add(id)
      return next
    })
  }

  function startEditing() {
    setSelected(new Set(activeIds))
    setError(undefined)
    setEditing(true)
  }

  async function save() {
    if (saving) return
    if (selected.size < MIN_TOPICS || selected.size > MAX_TOPICS) return setError(copy.countError)
    setSaving(true)
    setError(undefined)
    try {
      await api.setTopics({ topicIds: [...selected] })
      setEditing(false)
      onSaved()
    } catch (caught) {
      setError(saveError(caught, t))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Card id={id} className={cn('scroll-mt-4 xl:[contain:size]', onboarding && 'ring-2 ring-primary', className)}>
      <CardHeader>
        <CardTitle>
          <h2>{copy.title}</h2>
        </CardTitle>
        <CardDescription>{isEditing ? copy.pick : copy.subtitle}</CardDescription>
        {!isEditing && topics.length > 0 && (
          <CardAction>
            <Button variant="outline" size="sm" onClick={startEditing} disabled={queueLocked}>
              <Pencil aria-hidden="true" />
              {copy.edit}
            </Button>
          </CardAction>
        )}
      </CardHeader>

      {/* On xl the skill graph next to this card sets the row height ([contain:size]); the list scrolls inside. */}
      <CardContent className="flex min-h-0 flex-1 flex-col gap-4">
        {topics.length === 0 ? (
          <p className="text-sm text-muted-foreground">{t.dashboard.skills.empty}</p>
        ) : (
          <ul className="relative grid max-h-72 min-h-0 flex-1 content-start gap-2 overflow-y-auto [scrollbar-width:thin] sm:grid-cols-2 xl:max-h-none xl:grid-cols-1">
            {ordered.map(({ topic, active }) => {
              const Icon = topicIcon(topic.slug)
              const on = selected.has(topic.id)
              return isEditing ? (
                <li key={topic.id}>
                  <button
                    type="button"
                    aria-pressed={on}
                    disabled={queueLocked || (!on && selected.size >= MAX_TOPICS)}
                    onClick={() => toggle(topic.id)}
                    className={cn(
                      row,
                      'transition-colors focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-ring disabled:cursor-not-allowed disabled:opacity-40',
                      on
                        ? 'bg-primary/10 text-primary ring-primary'
                        : 'text-muted-foreground ring-border hover:text-foreground hover:ring-input',
                    )}
                  >
                    <Icon aria-hidden="true" className="size-4.5 shrink-0" />
                    <span className="truncate">{topic.name}</span>
                    {on && <Check aria-hidden="true" className="ml-auto size-4 shrink-0" />}
                  </button>
                </li>
              ) : (
                <li
                  key={topic.id}
                  className={cn(row, 'ring-border', active ? 'text-foreground' : 'text-muted-foreground')}
                >
                  <Icon aria-hidden="true" className={cn('size-4.5 shrink-0', active && 'text-primary')} />
                  <span className="truncate">{topic.name}</span>
                  {active && <span className="sr-only">, {t.dashboard.skills.active}</span>}
                </li>
              )
            })}
          </ul>
        )}
        {isEditing && topics.length > 0 && (
          <div className="flex flex-wrap items-center justify-between gap-3">
            <p aria-live="polite" className="font-mono text-xs text-muted-foreground">
              {copy.selected(selected.size)}
            </p>
            <div className="flex gap-2">
              {!onboarding && (
                <Button variant="ghost" onClick={() => setEditing(false)}>
                  {copy.cancel}
                </Button>
              )}
              <Button onClick={save} aria-disabled={saving}>
                {saving ? copy.saving : copy.save}
              </Button>
            </div>
          </div>
        )}
        {queueLocked && <p className="text-sm text-muted-foreground">{copy.locked}</p>}
        {error && (
          <p role="alert" className="text-sm text-destructive">
            {error}
          </p>
        )}
      </CardContent>
    </Card>
  )
}
