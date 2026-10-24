import { Eye, EyeOff } from 'lucide-react'
import { useId, useState, type ComponentProps } from 'react'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'

interface TextFieldProps extends ComponentProps<'input'> {
  label: string
  hint?: string
  error?: string
}

const iconSwap = 'col-start-1 row-start-1 transition-[opacity,scale,filter] duration-200 ease-[cubic-bezier(0.2,0,0,1)] motion-reduce:transition-none'
const iconHidden = 'scale-25 opacity-0 blur-[4px]'

// Label, input and one message line wired together for screen readers. type="password" adds a show/hide toggle.
// An error replaces the hint instead of adding a line, so the form keeps its height and still fits one screen.
export function TextField({ label, hint, error, type, className, ...input }: TextFieldProps) {
  const id = useId()
  const { t } = useLocale()
  const [revealed, setRevealed] = useState(false)
  const isPassword = type === 'password'
  const message = error ?? hint

  return (
    <div className={cn('grid gap-2', className)}>
      <Label htmlFor={id}>{label}</Label>
      <div className="relative">
        <Input
          id={id}
          type={isPassword && revealed ? 'text' : type}
          aria-invalid={error ? true : undefined}
          aria-describedby={message ? `${id}-message` : undefined}
          className={isPassword ? 'pr-11' : undefined}
          {...input}
        />
        {isPassword && (
          <button
            type="button"
            aria-label={t.auth.fields.showPassword}
            aria-pressed={revealed}
            aria-controls={id}
            onClick={() => setRevealed((value) => !value)}
            className="absolute inset-y-0 right-0 grid w-10 place-items-center rounded-r-lg text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-ring"
          >
            <Eye aria-hidden="true" className={cn(iconSwap, 'size-4', revealed && iconHidden)} />
            <EyeOff aria-hidden="true" className={cn(iconSwap, 'size-4', !revealed && iconHidden)} />
          </button>
        )}
      </div>
      {message && (
        <p id={`${id}-message`} className={cn('text-sm text-pretty', error ? 'text-destructive' : 'text-muted-foreground')}>
          {message}
        </p>
      )}
    </div>
  )
}
