import type { ReactNode } from 'react'

// Code as written (at most 25 lines of 60 characters), with line numbers. Scrolls sideways on narrow phones.
// `line` replaces how a line renders (find_bug makes them buttons); the cloze blank "____" is highlighted.
export function CodeBlock({ code, line }: { code: string; line?: (text: string, number: number) => ReactNode }) {
  return (
    <div className="overflow-x-auto rounded-lg bg-background py-3 font-mono text-[0.8125rem] leading-6 ring-1 ring-border ring-inset">
      <ol className="grid min-w-max">
        {code.split('\n').map((text, index) => (
          <li key={index} className="grid grid-cols-[2.75rem_1fr]">
            {line ? (
              line(text, index + 1)
            ) : (
              <>
                <span aria-hidden="true" className="pr-3 text-right text-muted-foreground/70 select-none">
                  {index + 1}
                </span>
                <code className="pr-4 whitespace-pre">{blanks(text)}</code>
              </>
            )}
          </li>
        ))}
      </ol>
    </div>
  )
}

function blanks(text: string) {
  const parts = text.split('____')
  return parts.flatMap((part, index) =>
    index === 0 ? [part] : [<mark key={index} className="rounded-sm bg-primary/20 px-1 text-primary">____</mark>, part],
  )
}
