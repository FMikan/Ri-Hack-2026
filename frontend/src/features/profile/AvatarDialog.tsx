import { Check } from 'lucide-react'
import { useState, type SubmitEvent } from 'react'
import { Button } from '@/components/ui/button'
import { Dialog, DialogClose, DialogContent } from '@/components/ui/dialog'
import { useAuth } from '@/features/auth/auth-context'
import { dialogFooter } from '@/features/play/dialog-footer'
import { DialogHeading } from '@/features/play/dialog-parts'
import { ApiError } from '@/lib/api-client'
import { AVATARS, avatarOf } from '@/lib/avatars'
import { useLocale } from '@/lib/i18n/locale-context'
import type { MeDto } from '@/types/api'

interface AvatarDialogProps {
  user: MeDto
  open: boolean
  onOpenChange: (open: boolean) => void
}

// Opened from the portrait on the profile: one of the 30 portraits, saved on the account so other players see it too.
export function AvatarDialog({ user, open, onOpenChange }: AvatarDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent showCloseButton={false} className="gap-5 bg-card p-5 sm:max-w-xl sm:p-6">
        <AvatarForm user={user} onDone={() => onOpenChange(false)} />
      </DialogContent>
    </Dialog>
  )
}

// Mounted only while the dialog is open, so every opening starts from the current portrait.
function AvatarForm({ user, onDone }: { user: MeDto; onDone: () => void }) {
  const { t } = useLocale()
  const copy = t.avatar
  const { updateMe } = useAuth()
  const [key, setKey] = useState(() => avatarOf(user).key)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string>()

  async function submit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    if (saving) return
    if (key === user.avatar) return onDone()
    setSaving(true)
    setError(undefined)
    try {
      await updateMe({ avatar: key })
      onDone()
    } catch (caught) {
      setError(caught instanceof ApiError && caught.status === 0 ? t.common.serverUnreachable : t.auth.errors.generic)
      setSaving(false)
    }
  }

  return (
    <>
      <DialogHeading title={copy.title} description={copy.subtitle} />
      <form className="grid gap-5" onSubmit={submit}>
        <div role="radiogroup" aria-label={copy.title} className="grid grid-cols-5 gap-3 sm:grid-cols-6">
          {AVATARS.map((avatar, index) => (
            <label
              key={avatar.key}
              className="group/avatar relative cursor-pointer rounded-full ring-1 ring-border transition-[box-shadow,scale] hover:ring-input active:scale-[0.96] has-checked:ring-2 has-checked:ring-primary has-focus-visible:outline-2 has-focus-visible:outline-offset-2 has-focus-visible:outline-ring motion-reduce:transition-none"
            >
              <input
                type="radio"
                name="avatar"
                value={avatar.key}
                checked={key === avatar.key}
                onChange={() => setKey(avatar.key)}
                aria-label={copy.option(index + 1)}
                className="sr-only"
              />
              <img src={avatar.url} alt="" draggable={false} className="aspect-square w-full rounded-full bg-secondary" />
              <span className="absolute -right-0.5 -bottom-0.5 hidden size-5 place-items-center rounded-full bg-primary text-primary-foreground ring-2 ring-card group-has-checked/avatar:grid">
                <Check aria-hidden="true" className="size-3.5" strokeWidth={3} />
              </span>
            </label>
          ))}
        </div>
        <div className={dialogFooter}>
          {error && (
            <p role="alert" className="mr-auto text-sm text-destructive">
              {error}
            </p>
          )}
          <DialogClose asChild>
            <Button type="button" variant="outline">
              {copy.cancel}
            </Button>
          </DialogClose>
          <Button type="submit" aria-disabled={saving}>
            {saving ? copy.saving : copy.save}
          </Button>
        </div>
      </form>
    </>
  )
}
