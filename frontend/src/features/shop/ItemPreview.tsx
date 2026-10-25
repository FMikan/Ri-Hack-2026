import { Snowflake } from 'lucide-react'
import { UserAvatar } from '@/components/shared/UserAvatar'
import { Badge } from '@/components/ui/badge'
import { avatarOf } from '@/lib/avatars'
import { bannerClass, boardClass, cardClass, nameClass, SLOTS } from '@/lib/cosmetics'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import type { MeDto, ShopItemDto, UserRefDto } from '@/types/api'

const RARITY_STYLES: Record<string, string> = {
  rare: 'text-primary',
  epic: 'border-tier-master/60 text-tier-master',
  legendary: 'border-warning/60 bg-warning/10 text-warning',
}

export function RarityBadge({ rarity }: { rarity: string }) {
  const { t } = useLocale()
  return (
    <Badge variant={rarity === 'common' ? 'secondary' : 'outline'} className={RARITY_STYLES[rarity]}>
      {t.shop.rarity[rarity as keyof typeof t.shop.rarity] ?? rarity}
    </Badge>
  )
}

// The item on me: my portrait in the frame, my name in the color, the pattern itself. Used by the shop and my locker.
export function ItemPreview({ item, me }: { item: ShopItemDto; me: MeDto }) {
  const { t } = useLocale()
  const slot = SLOTS[item.kind]
  const on: Pick<UserRefDto, 'equipment'> = { equipment: slot ? { [slot]: item.assetKey } : {} }

  switch (item.kind) {
    case 'avatar_frame':
      return <UserAvatar user={{ id: `preview-${item.id}`, avatar: avatarOf(me).key, ...on }} className="size-16" />
    case 'name_color':
      return <span className={cn('font-heading text-xl font-semibold', nameClass(on))}>{me.username}</span>
    case 'banner':
      return <span className={cn('size-full', bannerClass(on))} />
    case 'card_background':
      return (
        <span className={cn('grid size-full content-center gap-0.5 p-4', cardClass(on))}>
          <span className="font-heading font-semibold">{me.username}</span>
          <span className="text-xs text-muted-foreground">{t.dashboard.progress.levelValue(me.level)}</span>
        </span>
      )
    case 'board_theme':
      return (
        <span className={cn('flex size-full items-center justify-center gap-6', boardClass(on))}>
          <UserAvatar user={me} className="size-10" />
          <span className="font-mono text-xs text-muted-foreground">vs</span>
          <span className="size-10 rounded-full bg-secondary ring-1 ring-border" />
        </span>
      )
    default:
      return (
        <span className="grid justify-items-center gap-1 text-primary">
          <Snowflake aria-hidden="true" className="size-10" />
          <span className="sr-only">{t.shop.kinds.streak_freeze}</span>
        </span>
      )
  }
}
