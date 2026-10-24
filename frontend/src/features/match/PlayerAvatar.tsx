import { BotAvatar } from '@/components/shared/BotAvatar'
import { cn } from '@/lib/utils'
import type { PlayerDto } from '@/types/api'

// A bot shows its portrait; a person shows their initial on sky until avatars land in the shop (F4).
export function PlayerAvatar({ player, className }: { player?: PlayerDto; className?: string }) {
  if (player?.kind === 'bot' && player.persona) return <BotAvatar persona={player.persona} className={className} />
  return (
    <span
      aria-hidden="true"
      className={cn('grid shrink-0 place-items-center rounded-full bg-primary font-heading font-semibold text-primary-foreground', className)}
    >
      {player?.user?.username.charAt(0).toUpperCase()}
    </span>
  )
}
