import { X } from 'lucide-react'
import { Link } from 'react-router'
import { Button } from '@/components/ui/button'
import { DialogClose, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { useLocale } from '@/lib/i18n/locale-context'

export function DialogHeading({ title, description }: { title: string; description: string }) {
  const { t } = useLocale()
  return (
    <DialogHeader className="flex-row items-start gap-3 text-left">
      <div className="grid flex-1 gap-1">
        <DialogTitle className="font-heading text-xl font-semibold tracking-tight sm:text-2xl">{title}</DialogTitle>
        <DialogDescription className="text-pretty">{description}</DialogDescription>
      </div>
      <DialogClose asChild>
        <Button variant="ghost" size="icon" aria-label={t.common.close} className="-mt-1 -mr-2 shrink-0">
          <X aria-hidden="true" />
        </Button>
      </DialogClose>
    </DialogHeader>
  )
}

// A duel is still running (another tab, or the player left it): go back instead of starting a new one.
export function ResumeBanner({ matchId }: { matchId: string }) {
  const { t } = useLocale()
  return (
    <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg bg-primary/10 p-4 ring-1 ring-primary/40 ring-inset">
      <p className="font-medium">{t.play.inMatch}</p>
      <Button asChild>
        <Link to={`/match/${matchId}`}>{t.play.resume}</Link>
      </Button>
    </div>
  )
}
