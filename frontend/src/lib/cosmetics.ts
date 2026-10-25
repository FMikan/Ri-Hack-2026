import type { EquipmentSlot, ShopItemDto, ShopItemKind, UserRefDto } from '@/types/api'

// How the shop's assetKeys look (docs §6.11 F4b): each one is a class of the same name in src/cosmetics.css.
// UserRefDto.equipment carries them next to every name, so other players see them too. A key this build does not
// know draws nothing, so a new item on the server never breaks a page.
type Equipped = Pick<UserRefDto, 'equipment'>

// The cosmetic assetKeys in backend Seed/shop.json.
const KNOWN = new Set([
  'banner-backbone', 'banner-bitstream', 'banner-breadboard', 'banner-falling-code', 'banner-git-graph',
  'banner-gold-traces', 'banner-hex-dump', 'banner-kernel-panic', 'banner-night-shift', 'banner-oscilloscope',
  'banner-punch-card', 'banner-server-rack', 'banner-terminal',
  'board-blueprint', 'board-catppuccin', 'board-chalkboard', 'board-deep-space', 'board-devnull', 'board-dracula',
  'board-gruvbox', 'board-midnight', 'board-monokai', 'board-nord', 'board-phosphor', 'board-solarized',
  'board-tokyo-night',
  'card-backbone', 'card-blueprint', 'card-deprecated', 'card-dot-grid', 'card-hex-dump', 'card-hex-grid',
  'card-isometric', 'card-minimap', 'card-motherboard', 'card-punch-card', 'card-server-rack',
  'card-tabs-vs-spaces',
  'frame-bronze-ring', 'frame-circuit', 'frame-double-buffer', 'frame-draft-pr', 'frame-event-loop',
  'frame-exit-zero', 'frame-kernel-panic', 'frame-merge-conflict', 'frame-phosphor', 'frame-season-bronze',
  'frame-season-diamond', 'frame-season-gold', 'frame-season-master', 'frame-season-platinum',
  'frame-season-silver', 'frame-still-compiling', 'frame-syntax-highlight',
  'name-amber', 'name-blue-screen', 'name-coolant', 'name-copper-trace', 'name-gold-master', 'name-green-build',
  'name-hot-reload', 'name-hotfix', 'name-keyword', 'name-phosphor', 'name-rainbow-table', 'name-screen-tear',
  'name-sudo',
])

const pick = (key?: string) => (key && KNOWN.has(key) ? key : undefined)

export const frameClass = (user: Partial<Equipped>) => pick(user.equipment?.avatarFrame)
export const nameClass = (user: Partial<Equipped>) => pick(user.equipment?.nameColor)
// Profile header strip, the profile card behind it, and the duel screen.
export const bannerClass = (user: Equipped) => pick(user.equipment.banner)
export const cardClass = (user: Equipped) => pick(user.equipment.cardBackground)
export const boardClass = (user?: Partial<Equipped> | null) => pick(user?.equipment?.boardTheme)

// The equipment slot each kind of item goes into (PUT /api/me/equipment); the streak freeze is used up, not worn.
export const SLOTS: Partial<Record<ShopItemKind, EquipmentSlot>> = {
  avatar_frame: 'avatarFrame',
  banner: 'banner',
  board_theme: 'boardTheme',
  name_color: 'nameColor',
  card_background: 'cardBackground',
}

// The shop and my locker show items in this order, one section per kind that has any.
const KIND_ORDER: ShopItemKind[] = ['avatar_frame', 'name_color', 'banner', 'card_background', 'board_theme', 'streak_freeze']
export const byKind = (items: ShopItemDto[]) =>
  KIND_ORDER.map((kind) => ({ kind, items: items.filter((item) => item.kind === kind) })).filter((section) => section.items.length > 0)
