import { useAuth } from '@/features/auth/auth-context'
import { avatarOf } from '@/lib/avatars'
import { frameClass } from '@/lib/cosmetics'
import { cn } from '@/lib/utils'
import type { UserRefDto } from '@/types/api'

type AvatarUser = Pick<UserRefDto, 'id' | 'avatar'> & Partial<Pick<UserRefDto, 'equipment'>>

// A player's portrait inside their equipped frame (the frame draws its own edge, so the plain ring is only for none). My own comes from the session, so a new pick shows everywhere at once.
export function UserAvatar({ user, className }: { user: AvatarUser; className?: string }) {
  const { session } = useAuth()
  const self = session.status === 'authenticated' && session.user.id === user.id ? session.user : user
  return (
    <span
      aria-hidden="true"
      className={cn('block shrink-0 overflow-hidden rounded-full bg-secondary', frameClass(self) ?? 'ring-1 ring-border', className)}
    >
      <img src={avatarOf(self).url} alt="" draggable={false} className="size-full" />
    </span>
  )
}
