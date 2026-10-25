import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'

// A duel seat: the portrait size shared by matchmaking and the lobby, and the duel badge between them.
export const seat = 'size-16 sm:size-20'

// A seat's sky ring, drawn where the duel badge draws its own (2px outside the circle) so all three line up. The portrait
// sits inside with a gap (absolute inset-1.5).
export function SeatFrame({ children }: { children: ReactNode }) {
  return (
    <span aria-hidden="true" className={cn('relative grid place-items-center', seat)}>
      <span className="absolute -inset-0.5 rounded-full border-2 border-primary" />
      {children}
    </span>
  )
}
