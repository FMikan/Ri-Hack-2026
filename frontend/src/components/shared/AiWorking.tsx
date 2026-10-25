import { GraduationCap, LoaderCircle } from 'lucide-react'
import { Dialog, DialogContent, DialogDescription, DialogTitle } from '@/components/ui/dialog'

interface AiWorkingProps {
  open: boolean
  title: string
  description: string
}

// Holds the page while the AI Professor writes something the next screen needs (a lesson takes a few seconds), so a
// slow answer reads as work in progress instead of a button that did nothing. It cannot be dismissed: it closes when
// the request settles.
export function AiWorking({ open, title, description }: AiWorkingProps) {
  return (
    <Dialog open={open}>
      <DialogContent
        showCloseButton={false}
        onEscapeKeyDown={(event) => event.preventDefault()}
        onPointerDownOutside={(event) => event.preventDefault()}
        className="justify-items-center gap-3 p-6 text-center"
      >
        <span className="relative grid size-16 place-items-center rounded-full bg-primary/10 text-primary">
          <GraduationCap aria-hidden="true" className="size-7" />
          <LoaderCircle aria-hidden="true" className="absolute inset-0 size-16 animate-spin stroke-1 motion-reduce:animate-none" />
        </span>
        <DialogTitle className="text-lg leading-snug">{title}</DialogTitle>
        <DialogDescription className="text-pretty">{description}</DialogDescription>
      </DialogContent>
    </Dialog>
  )
}
