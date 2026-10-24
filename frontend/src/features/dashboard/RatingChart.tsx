import { useState } from 'react'
import { useLocale } from '@/lib/i18n/locale-context'

interface Point {
  at: string
  rating: number
}

const STEPS = [10, 25, 50, 100, 200, 500]

// Round axis bounds to a clean step with at most five gridlines.
function axis(values: number[]) {
  const min = Math.min(...values)
  const max = Math.max(...values)
  const step = STEPS.find((candidate) => (Math.ceil(max / candidate) - Math.floor(min / candidate)) <= 4) ?? 1000
  const low = Math.floor(min / step) * step
  const high = Math.max(Math.ceil(max / step) * step, low + step)
  const ticks = Array.from({ length: (high - low) / step + 1 }, (_, index) => low + index * step)
  return { low, high, ticks }
}

// Single-series area chart: 2px sky line, 10% wash, hairline grid, crosshair and tooltip on hover.
// Lines live in a stretched SVG; text and dots are HTML placed in percent so they never distort.
export function RatingChart({ points }: { points: Point[] }) {
  const { t, locale } = useLocale()
  const copy = t.dashboard.rating
  const [hovered, setHovered] = useState<number>()

  if (points.length < 2) {
    return (
      <div className="relative grid h-52 place-items-center">
        <div aria-hidden="true" className="absolute inset-0 flex flex-col justify-between">
          {[0, 1, 2, 3, 4].map((line) => (
            <span key={line} className="border-t border-border" />
          ))}
        </div>
        <p className="relative max-w-56 bg-card px-3 py-1 text-center text-sm text-pretty text-muted-foreground">{copy.empty}</p>
      </div>
    )
  }

  const { low, high, ticks } = axis(points.map((point) => point.rating))
  const last = points.length - 1
  const x = (index: number) => (index / last) * 100
  const y = (rating: number) => (1 - (rating - low) / (high - low)) * 100
  const line = points.map((point, index) => `${index ? 'L' : 'M'}${x(index)} ${y(point.rating)}`).join(' ')
  const day = new Intl.DateTimeFormat(locale, { day: '2-digit', month: 'short' })
  const labelIndexes = [...new Set([0, Math.round(last / 3), Math.round((last * 2) / 3), last])]
  const shown = hovered ?? last

  return (
    <figure className="grid grid-cols-[auto_minmax(0,1fr)] gap-x-3 gap-y-2">
      <figcaption className="sr-only">{copy.chart(points[0].rating, points[last].rating, points.length)}</figcaption>

      <div aria-hidden="true" className="relative h-52 font-mono text-xs text-muted-foreground tabular-nums">
        <span className="invisible">{high}</span>
        {ticks.map((tick) => (
          <span key={tick} className="absolute right-0 -translate-y-1/2" style={{ top: `${y(tick)}%` }}>
            {tick}
          </span>
        ))}
      </div>

      <div
        aria-hidden="true"
        className="relative h-52 touch-none"
        onPointerMove={(event) => {
          const box = event.currentTarget.getBoundingClientRect()
          setHovered(Math.round(Math.min(1, Math.max(0, (event.clientX - box.left) / box.width)) * last))
        }}
        onPointerLeave={() => setHovered(undefined)}
      >
        <svg viewBox="0 0 100 100" preserveAspectRatio="none" className="absolute inset-0 size-full overflow-visible">
          {ticks.map((tick) => (
            <line key={tick} x1="0" x2="100" y1={y(tick)} y2={y(tick)} className="stroke-border" strokeWidth={1} vectorEffect="non-scaling-stroke" />
          ))}
          <path d={`${line} L100 100 L0 100 Z`} className="fill-chart-1/10" />
          <path
            d={line}
            fill="none"
            className="stroke-chart-1"
            strokeWidth={2}
            strokeLinecap="round"
            strokeLinejoin="round"
            vectorEffect="non-scaling-stroke"
          />
        </svg>
        {hovered !== undefined && <span className="absolute inset-y-0 w-px bg-input" style={{ left: `${x(hovered)}%` }} />}
        <span
          className="absolute size-2.5 -translate-1/2 rounded-full bg-chart-1 ring-2 ring-card"
          style={{ left: `${x(shown)}%`, top: `${y(points[shown].rating)}%` }}
        />
        {hovered !== undefined && (
          <span
            className="absolute z-10 grid -translate-x-1/2 -translate-y-[calc(100%+0.75rem)] gap-0.5 rounded-md bg-popover px-2.5 py-1.5 text-xs whitespace-nowrap ring-1 ring-border"
            style={{ left: `${Math.min(88, Math.max(12, x(hovered)))}%`, top: `${y(points[hovered].rating)}%` }}
          >
            <span className="text-muted-foreground">{day.format(new Date(points[hovered].at))}</span>
            <span className="font-mono font-medium tabular-nums">{points[hovered].rating}</span>
          </span>
        )}
      </div>

      <span />
      <div aria-hidden="true" className="relative h-4 font-mono text-xs text-muted-foreground">
        {labelIndexes.map((index) => (
          <span
            key={index}
            className="absolute whitespace-nowrap"
            style={{
              left: `${x(index)}%`,
              transform: index === 0 ? 'none' : index === last ? 'translateX(-100%)' : 'translateX(-50%)',
            }}
          >
            {day.format(new Date(points[index].at))}
          </span>
        ))}
      </div>

      <table className="sr-only">
        <thead>
          <tr>
            <th>{copy.date}</th>
            <th>{copy.title}</th>
          </tr>
        </thead>
        <tbody>
          {points.map((point) => (
            <tr key={point.at}>
              <td>{day.format(new Date(point.at))}</td>
              <td>{point.rating}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </figure>
  )
}
