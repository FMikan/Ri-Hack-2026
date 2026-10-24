import { cn } from '@/lib/utils'

interface SegmentedControlProps<T extends string> {
  label: string
  name: string
  value: T
  options: { value: T; label: string }[]
  onChange: (value: T) => void
  className?: string
}

// A row of radios that looks like a switch (practice mode, leaderboard tabs). Wraps on narrow screens so no option hides.
export function SegmentedControl<T extends string>({ label, name, value, options, onChange, className }: SegmentedControlProps<T>) {
  return (
    <div role="radiogroup" aria-label={label} className={cn('flex max-w-full flex-wrap gap-1 rounded-lg bg-secondary p-1', className)}>
      {options.map((option) => (
        <label
          key={option.value}
          className="relative flex-1 cursor-pointer rounded-md px-3.5 py-2 text-center text-sm font-medium whitespace-nowrap text-muted-foreground transition-colors hover:text-foreground has-checked:bg-primary/15 has-checked:text-primary has-focus-visible:outline-2 has-focus-visible:-outline-offset-2 has-focus-visible:outline-ring max-lg:py-3"
        >
          <input
            type="radio"
            name={name}
            value={option.value}
            checked={value === option.value}
            onChange={() => onChange(option.value)}
            className="sr-only"
          />
          {option.label}
        </label>
      ))}
    </div>
  )
}
