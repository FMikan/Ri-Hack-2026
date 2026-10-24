import type { EquipmentSlot, ShopItemKind, UserRefDto } from '@/types/api'

// How the shop's assetKeys look (docs §6.11 F4b). UserRefDto.equipment carries them next to every name, so other
// players see them too. An unknown key draws nothing, so a new item on the server never breaks a page.
type Equipped = Pick<UserRefDto, 'equipment'>

const FRAMES: Record<string, string> = {
  'frame-bronze-ring': 'ring-[3px] ring-medal-bronze',
  'frame-circuit': 'ring-2 ring-primary outline-2 outline-offset-2 outline-dashed outline-primary/60',
  'frame-season-bronze': 'ring-[3px] ring-medal-bronze outline-2 outline-offset-2 outline-medal-bronze/50',
  'frame-season-silver': 'ring-[3px] ring-medal-silver outline-2 outline-offset-2 outline-medal-silver/50',
  'frame-season-gold': 'ring-[3px] ring-medal-gold outline-2 outline-offset-2 outline-medal-gold/50',
  'frame-season-platinum': 'ring-[3px] ring-tier-platinum outline-2 outline-offset-2 outline-tier-platinum/50',
  'frame-season-diamond': 'ring-[3px] ring-primary outline-2 outline-offset-2 outline-primary/50',
  'frame-season-master': 'ring-[3px] ring-tier-master outline-2 outline-offset-2 outline-tier-master/50',
}
const NAME_COLORS: Record<string, string> = { 'name-amber': 'text-warning' }
// Profile header strip, the profile card behind it, and the duel screen.
const BANNERS: Record<string, string> = { 'banner-terminal': 'banner-terminal' }
const CARD_BACKGROUNDS: Record<string, string> = { 'card-blueprint': 'card-blueprint' }
const BOARDS: Record<string, string> = { 'board-midnight': 'bg-board-midnight' }

const pick = (map: Record<string, string>, key?: string) => (key ? map[key] : undefined)

export const frameClass = (user: Partial<Equipped>) => pick(FRAMES, user.equipment?.avatarFrame)
export const nameClass = (user: Partial<Equipped>) => pick(NAME_COLORS, user.equipment?.nameColor)
export const bannerClass = (user: Equipped) => pick(BANNERS, user.equipment.banner)
export const cardClass = (user: Equipped) => pick(CARD_BACKGROUNDS, user.equipment.cardBackground)
export const boardClass = (user?: Partial<Equipped> | null) => pick(BOARDS, user?.equipment?.boardTheme)

// The equipment slot each kind of item goes into (PUT /api/me/equipment); the streak freeze is used up, not worn.
export const SLOTS: Partial<Record<ShopItemKind, EquipmentSlot>> = {
  avatar_frame: 'avatarFrame',
  banner: 'banner',
  board_theme: 'boardTheme',
  name_color: 'nameColor',
  card_background: 'cardBackground',
}
