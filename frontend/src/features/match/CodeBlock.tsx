import { useMemo, type ReactNode } from 'react'
import { highlightLines } from './highlight'

interface CodeBlockProps {
  code: string
  language?: string | null // QuestionPublicDto.language; colors the code by its syntax
  line?: (content: ReactNode, number: number) => ReactNode
}

// Code as written (at most 25 lines of 60 characters), with line numbers and syntax colors. Scrolls sideways on
// narrow phones. `line` replaces how a line renders (find_bug makes them buttons); the cloze blank "____" is highlighted.
export function CodeBlock({ code, language, line }: CodeBlockProps) {
  const lines = useMemo(() => highlightLines(code, language), [code, language])

  return (
    <div className="overflow-x-auto rounded-lg bg-background py-3 font-mono text-[0.8125rem] leading-6 ring-1 ring-border ring-inset">
      <ol className="grid min-w-max">
        {lines.map((tokens, index) => {
          const content = tokens.map((token, key) => (
            <span key={key} className={token.tone}>
              {blanks(token.text)}
            </span>
          ))
          return (
            <li key={index} className="grid grid-cols-[2.75rem_1fr]">
              {line ? (
                line(content, index + 1)
              ) : (
                <>
                  <span aria-hidden="true" className="pr-3 text-right text-muted-foreground/70 select-none">
                    {index + 1}
                  </span>
                  <code className="pr-4 whitespace-pre">{content}</code>
                </>
              )}
            </li>
          )
        })}
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
