import { BotAvatar } from '@/components/shared/BotAvatar'
import { UserAvatar } from '@/components/shared/UserAvatar'
import { cn } from '@/lib/utils'
import type { PlayerDto } from '@/types/api'

// A bot shows its portrait, a person (or their ghost) the one they picked; an empty circle while the seat loads.
export function PlayerAvatar({ player, className }: { player?: PlayerDto; className?: string }) {
  if (player?.kind === 'bot' && player.persona) return <BotAvatar persona={player.persona} className={className} />
  if (player?.user) return <UserAvatar user={player.user} className={className} />
  return <span aria-hidden="true" className={cn('block shrink-0 rounded-full bg-secondary', className)} />
}
