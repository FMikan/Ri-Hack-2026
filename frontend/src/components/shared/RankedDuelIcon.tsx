import { useId, type SVGProps } from 'react'
import { cn } from '@/lib/utils'

// The ranked duel mark from the brand kit (Setup/Branding Assets/icon-ranked-duel.svg): two crossed swords, the back one
// cut where the front one crosses it. Inline so it takes currentColor; ids are per instance because of the mask.
export function RankedDuelIcon(props: SVGProps<SVGSVGElement>) {
  const id = useId().replace(/[^\w-]/g, '')
  const blade = `${id}-blade`
  const sword = `${id}-sword`
  const cut = `${id}-cut`
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
          <use href={`#${blade}`} transform="rotate(-45 12 12)" fill="#000" stroke="#000" strokeWidth={5} />
        </mask>
      </defs>
      <g mask={`url(#${cut})`}>
        <use href={`#${sword}`} transform="rotate(45 12 12)" />
      </g>
      <use href={`#${sword}`} transform="rotate(-45 12 12)" />
    </svg>
  )
}

// The duel mark in a dark disc with a sky ring, matching how the bots are framed (BotAvatar).
export function DuelBadge({ className }: { className?: string }) {
  return (
    <span className={cn('grid shrink-0 place-items-center rounded-full bg-secondary text-primary ring-2 ring-primary', className)}>
      <RankedDuelIcon className="size-3/5" />
    </span>
  )
}
