import type { ReactNode, SVGProps } from 'react'
import { cn } from '@/lib/utils'
import type { Tier } from '@/types/api'

// Each tier in its own color: the leaderboard medals for the first three, the season frames for platinum and master.
const COLORS: Record<Tier, string> = {
  bronze: 'text-medal-bronze',
  silver: 'text-medal-silver',
  gold: 'text-medal-gold',
  platinum: 'text-tier-platinum',
  diamond: 'text-primary',
  master: 'text-tier-master',
  grandmaster: 'text-foreground',
}

const CROWN = (
  <>
    <path d="M6.5 15 5.5 8.5l3.75 3L12 7l2.75 4.5 3.75-3-1 6.5z" fill="currentColor" />
    <path d="M7 17.5h10" />
  </>
)

// The mark grows with the tier: one, two, three chevrons, then a hexagon, a gem, a crown and a crown with a star.
const MARKS: Record<Tier, ReactNode> = {
  bronze: <path d="m8 14 4-4 4 4" />,
  silver: <path d="m8 12 4-4 4 4m-8 4.5 4-4 4 4" />,
  gold: <path d="m9 9.75 3-3 3 3m-6 3.75 3-3 3 3m-6 3.75 3-3 3 3" />,
  platinum: (
    <>
      <path d="M12 5.5 17.6 8.75v6.5L12 18.5l-5.6-3.25v-6.5z" />
      <path d="m9.5 13.25 2.5-2.5 2.5 2.5" />
    </>
  ),
  diamond: <path d="M8.7 7h6.6l2.2 3.3L12 17.5l-5.5-7.2zm2.75 0L9.8 10.3l2.2 7.2 2.2-7.2L12.55 7M6.5 10.3h11" strokeWidth={1.5} />,
  master: CROWN,
  grandmaster: (
    <>
      <path d="m12 3.75.7 1.3 1.3.7-1.3.7-.7 1.3-.7-1.3-1.3-.7 1.3-.7z" fill="currentColor" stroke="none" className="text-medal-gold" />
      <g transform="translate(0 2)">{CROWN}</g>
    </>
  ),
}

// A tier's badge: a tinted rounded square in the tier color with the tier's mark. Decorative, so always beside the name.
export function TierIcon({ tier, className, ...props }: SVGProps<SVGSVGElement> & { tier: Tier }) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.75}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      className={cn('shrink-0', COLORS[tier], className)}
      {...props}
    >
      <rect x="2" y="2" width="20" height="20" rx="5" fill="currentColor" fillOpacity={0.12} strokeWidth={1.5} />
      {MARKS[tier]}
    </svg>
  )
}
