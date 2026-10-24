import junior from '@/assets/bots/junior.svg'
import mid from '@/assets/bots/mid.svg'
import senior from '@/assets/bots/senior.svg'
import vibeCoder from '@/assets/bots/vibe_coder.svg'
import { cn } from '@/lib/utils'
import type { BotPersona } from '@/types/api'

const AVATARS: Record<BotPersona, string> = { vibe_coder: vibeCoder, junior, mid, senior }
// A ring in the bot's own accent color sets the portrait off the dark surface and tells the bots apart at a glance.
const RINGS: Record<BotPersona, string> = {
  vibe_coder: 'ring-bot-vibe-coder',
  junior: 'ring-bot-junior',
  mid: 'ring-bot-mid',
  senior: 'ring-bot-senior',
}

// The bot portraits from the brand kit (the brighter set in Setup/Branding Assets/stackoff-bot-avatars (2)), cropped
// to a circle on the raised surface. The art keeps the face inside a centered circle of radius 100 on its 256 canvas,
// so it is scaled 256 / 200 to fill ours. Decorative: the bot's name always sits next to it.
export function BotAvatar({ persona, className }: { persona: BotPersona; className?: string }) {
  return (
    <span aria-hidden="true" className={cn('block shrink-0 overflow-hidden rounded-full bg-secondary ring-2', RINGS[persona], className)}>
      <img src={AVATARS[persona]} alt="" draggable={false} className="size-full scale-[1.28]" />
    </span>
  )
}
