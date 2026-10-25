import { useId, type SVGProps } from 'react'
import { cn } from '@/lib/utils'

// Wrappers around each sword: the swing is a delta on top of the crossed pose, so without it (or with reduced motion)
// the mark is the static one.
const swing = 'origin-center motion-reduce:animate-none'

// The ranked duel mark from the brand kit (Setup/Branding Assets/icon-ranked-duel.svg): two crossed swords, the back one
// cut where the front one crosses it. Inline so it takes currentColor; ids are per instance because of the mask.
// `clash` loops the swords hitting every few seconds (index.css, clash-*); the cut follows the front sword as it swings.
export function RankedDuelIcon({ clash = false, ...props }: SVGProps<SVGSVGElement> & { clash?: boolean }) {
  const id = useId().replace(/[^\w-]/g, '')
  const blade = `${id}-blade`
  const sword = `${id}-sword`
  const cut = `${id}-cut`
  const front = cn(clash && 'animate-clash-front', swing)
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      {...props}
    >
      <defs>
        <path id={blade} d="M12-.5 13.6 2.8v15.8h-3.2V2.8z" />
        <g id={sword}>
          <use href={`#${blade}`} />
          <path d="M8 19.2h8M12 19.7v2.8" />
          <circle cx="12" cy="24" r="1.4" fill="currentColor" stroke="none" />
        </g>
        <mask id={cut} maskUnits="userSpaceOnUse" x="0" y="0" width="24" height="24">
          <rect width="24" height="24" fill="#fff" stroke="none" />
          <g className={front}>
            <use href={`#${blade}`} transform="rotate(-45 12 12)" fill="#000" stroke="#000" strokeWidth={5} />
          </g>
        </mask>
      </defs>
      <g mask={`url(#${cut})`}>
        <g className={cn(clash && 'animate-clash-back', swing)}>
          <use href={`#${sword}`} transform="rotate(45 12 12)" />
        </g>
      </g>
      <g className={front}>
        <use href={`#${sword}`} transform="rotate(-45 12 12)" />
      </g>
      {clash && (
        // Sparks in the four gaps around the crossing; hidden unless the animation runs.
        <path
          d="M12 4.5V2M12 19.5V22M4.5 12H2M19.5 12H22"
          strokeWidth={1.5}
          opacity={0}
          className="origin-center animate-clash-spark motion-reduce:hidden"
        />
      )}
    </svg>
  )
}

// The duel mark in a dark disc with a sky ring, matching how the bots are framed (BotAvatar).
export function DuelBadge({ className, clash }: { className?: string; clash?: boolean }) {
  return (
    <span className={cn('grid shrink-0 place-items-center rounded-full bg-secondary text-primary ring-2 ring-primary', className)}>
      <RankedDuelIcon clash={clash} className="size-3/5" />
    </span>
  )
}
