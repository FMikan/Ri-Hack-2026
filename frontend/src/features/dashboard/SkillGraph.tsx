import { useState } from 'react'
import { cn } from '@/lib/utils'
import { topicIcon } from './topic-icons'

export interface Skill {
  id: string
  slug: string
  name: string
  value: number | null // mastery 0–100, null when not played
  active: boolean
}

const RINGS = [1, 0.75, 0.5, 0.25]
const ICON_RADIUS = 1.2
const HALF = 1.42 // half the viewBox: room for the icon labels outside the outer ring

// Radar of mastery per topic: banded rings, dotted axes, one filled sky polygon. The SVG is square and uniformly
// scaled, so circles stay round; icons and the tooltip are HTML placed in percent of the same box.
export function SkillGraph({ skills, label, format }: { skills: Skill[]; label: string; format: (skill: Skill) => string }) {
  const [hovered, setHovered] = useState<number>()
  const count = skills.length
  const point = (index: number, radius: number) => {
    const angle = -Math.PI / 2 + (index * 2 * Math.PI) / count
    return [Math.cos(angle) * radius, Math.sin(angle) * radius] as const
  }
  const polygon = (radius: (index: number) => number) => skills.map((_, index) => point(index, radius(index)).join(',')).join(' ')
  const place = ([x, y]: readonly [number, number]) => ({
    left: `${((x + HALF) / (2 * HALF)) * 100}%`,
    top: `${((y + HALF) / (2 * HALF)) * 100}%`,
  })
  const value = (index: number) => Math.max((skills[index].value ?? 0) / 100, 0.02)

  return (
    <div className="relative mx-auto aspect-square w-full max-w-sm" onPointerLeave={() => setHovered(undefined)}>
      <svg viewBox={`${-HALF} ${-HALF} ${2 * HALF} ${2 * HALF}`} role="img" aria-label={label} className="size-full overflow-visible">
        {RINGS.map((ring, index) => (
          <polygon
            key={ring}
            points={polygon(() => ring)}
            className={cn('stroke-border', index % 2 ? 'fill-foreground/[0.03]' : 'fill-foreground/[0.06]')}
            strokeWidth={1}
            vectorEffect="non-scaling-stroke"
          />
        ))}
        {skills.map((skill, index) => {
          const [x, y] = point(index, 1.06)
          return (
            <line
              key={skill.id}
              x1={0}
              y1={0}
              x2={x}
              y2={y}
              className="stroke-muted-foreground/70"
              strokeWidth={1.5}
              strokeDasharray="0.1 5"
              strokeLinecap="round"
              vectorEffect="non-scaling-stroke"
            />
          )
        })}
        <polygon
          points={polygon(value)}
          className="fill-chart-1/30 stroke-chart-1"
          strokeWidth={2}
          strokeLinejoin="round"
          vectorEffect="non-scaling-stroke"
        />
        {skills.map((skill, index) => {
          const [x, y] = point(index, value(index))
          return (
            <g key={skill.id} onPointerEnter={() => setHovered(index)}>
              <circle cx={x} cy={y} r={0.14} className="fill-transparent" />
              <circle
                cx={x}
                cy={y}
                r={hovered === index ? 0.05 : 0.035}
                className="fill-chart-1 stroke-card"
                strokeWidth={2}
                vectorEffect="non-scaling-stroke"
              />
            </g>
          )
        })}
      </svg>

      {skills.map((skill, index) => {
        const Icon = topicIcon(skill.slug)
        return (
          <span
            key={skill.id}
            aria-hidden="true"
            onPointerEnter={() => setHovered(index)}
            className={cn(
              'absolute grid size-8 -translate-1/2 place-items-center rounded-full transition-colors',
              skill.active ? 'bg-primary/15 text-primary' : 'text-muted-foreground',
              hovered === index && 'bg-secondary text-foreground',
            )}
            style={place(point(index, ICON_RADIUS))}
          >
            <Icon className="size-4.5" />
          </span>
        )
      })}

      {hovered !== undefined && (
        <span
          aria-hidden="true"
          className="pointer-events-none absolute z-10 -translate-x-1/2 -translate-y-[calc(100%+0.75rem)] rounded-md bg-popover px-2.5 py-1.5 text-xs whitespace-nowrap ring-1 ring-border"
          style={place(point(hovered, value(hovered)))}
        >
          <span className="font-medium">{skills[hovered].name}</span>
          <span className="ml-2 font-mono text-muted-foreground tabular-nums">{format(skills[hovered])}</span>
        </span>
      )}
    </div>
  )
}
