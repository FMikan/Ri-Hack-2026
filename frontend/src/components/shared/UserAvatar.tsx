import { useAuth } from '@/features/auth/auth-context'
import { avatarOf } from '@/lib/avatars'
import { cn } from '@/lib/utils'
import type { UserRefDto } from '@/types/api'

// A player's portrait. My own comes from the session, so a new pick shows everywhere at once.
export function UserAvatar({ user, className }: { user: Pick<UserRefDto, 'id' | 'avatar'>; className?: string }) {
  const { session } = useAuth()
  const self = session.status === 'authenticated' && session.user.id === user.id ? session.user : user
  return (
    <span aria-hidden="true" className={cn('block shrink-0 overflow-hidden rounded-full bg-secondary ring-1 ring-border', className)}>
      <img src={avatarOf(self).url} alt="" draggable={false} className="size-full" />
    </span>
  )
}
