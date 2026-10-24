import type { UserRefDto } from '@/types/api'

// The 30 player portraits from Setup/Branding Assets/stackoff-player-avatars. The key ("hoodie_hacker") is what
// PATCH /api/me stores in `avatar`; files are numbered so the picker keeps the brand kit's order.
const FILES = import.meta.glob<string>('@/assets/avatars/*.svg', { eager: true, import: 'default' })

export const AVATARS = Object.keys(FILES)
  .sort()
  .map((path) => ({ key: path.replace(/^.*\/\d+-|\.svg$/g, ''), url: FILES[path] }))

// The picked portrait, or for players who never picked one ('default') a stable "random" one from their id.
export function avatarOf(user: Pick<UserRefDto, 'id' | 'avatar'>) {
  const picked = AVATARS.find((avatar) => avatar.key === user.avatar)
  if (picked) return picked
  const hash = [...user.id].reduce((sum, char) => (sum * 31 + char.charCodeAt(0)) | 0, 0)
  return AVATARS[Math.abs(hash) % AVATARS.length]
}
