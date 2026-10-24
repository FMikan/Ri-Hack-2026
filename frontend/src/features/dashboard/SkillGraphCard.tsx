import { Badge } from '@/components/ui/badge'
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import type { DashboardTopicDto } from '@/types/api'
import { SkillGraph, type Skill } from './SkillGraph'
import { topicIcon } from './topic-icons'

// Radar of mastery per topic with the same numbers as a list next to it: the list names every axis (the radar
// only has icons) and doubles as the accessible table.
export function SkillGraphCard({ topics, className }: { topics: DashboardTopicDto[]; className?: string }) {
  const { t } = useLocale()
  const copy = t.dashboard.skills
  const skills: Skill[] = topics.map(({ topic, active, mastery }) => ({
    id: topic.id,
    slug: topic.slug,
    name: topic.name,
    value: mastery ?? null,
    active,
  }))
  const format = (skill: Skill) => (skill.value == null ? copy.notPlayed : `${Math.round(skill.value)}%`)
  const played = skills.filter((skill) => skill.value != null).length

  return (
    <Card className={className}>
      <CardHeader>
        <CardTitle>
          <h2>{copy.title}</h2>
        </CardTitle>
        <CardDescription>{copy.subtitle}</CardDescription>
        {skills.length > 0 && (
          <CardAction>
            <span className="font-mono text-xs text-muted-foreground">{copy.played(played, skills.length)}</span>
          </CardAction>
        )}
      </CardHeader>
      <CardContent>
        {skills.length === 0 ? (
          <p className="text-sm text-muted-foreground">{copy.empty}</p>
        ) : (
          <div className="grid items-center gap-x-8 gap-y-6 md:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
            {skills.length >= 3 && <SkillGraph skills={skills} label={copy.chart} format={format} />}
            <ul className="grid gap-1">
              {skills.map((skill) => {
                const Icon = topicIcon(skill.slug)
                return (
                  <li key={skill.id} className="flex min-w-0 items-center gap-3 rounded-lg px-2 py-1.5 text-sm">
                    <Icon
                      aria-hidden="true"
                      className={cn('size-4 shrink-0', skill.active ? 'text-primary' : 'text-muted-foreground')}
                    />
                    <span className="truncate font-medium">{skill.name}</span>
                    {skill.active && <Badge variant="outline">{copy.active}</Badge>}
                    <span className="ml-auto shrink-0 font-mono text-xs text-muted-foreground tabular-nums">
                      {format(skill)}
                    </span>
                  </li>
                )
              })}
            </ul>
          </div>
        )}
      </CardContent>
    </Card>
  )
}
