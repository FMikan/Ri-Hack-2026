import { Fragment } from 'react'
import { CodeBlock } from '@/features/match/CodeBlock'

type Block =
  | { kind: 'code'; code: string; language?: string }
  | { kind: 'list'; ordered: boolean; start: number; items: string[] }
  | { kind: 'text'; lines: string[] }

// The subset the professor is told to write (ClaudeAi.ProfessorSystem): paragraphs, "- " and "1. " lists, **bold**,
// `code` and ``` fences. Everything renders as text nodes, never as HTML. An open fence (an answer still streaming)
// shows as a code block with the lines so far; a stray heading shows as a plain line.
function parse(text: string): Block[] {
  const blocks: Block[] = []
  const lines = text.replace(/\r\n/g, '\n').split('\n')
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i]
    const last = blocks.at(-1)
    const fence = /^\s*```\s*([\w#+.-]*)\s*$/.exec(line)
    if (fence) {
      const code: string[] = []
      for (i++; i < lines.length && !/^\s*```\s*$/.test(lines[i]); i++) code.push(lines[i])
      blocks.push({ kind: 'code', code: code.join('\n'), language: fence[1] || undefined })
      continue
    }
    const item = /^\s*(?:[-*•]|(\d+)[.)])\s+(.*)$/.exec(line)
    if (item) {
      const ordered = item[1] !== undefined
      if (last?.kind === 'list' && last.ordered === ordered) last.items.push(item[2])
      else blocks.push({ kind: 'list', ordered, start: ordered ? Number(item[1]) : 1, items: [item[2]] })
      continue
    }
    if (!line.trim()) {
      if (last?.kind === 'text' || last?.kind === 'list') blocks.push({ kind: 'text', lines: [] }) // ends the paragraph
      continue
    }
    const plain = line.replace(/^#{1,6}\s+/, '')
    if (last?.kind === 'text') last.lines.push(plain)
    else blocks.push({ kind: 'text', lines: [plain] })
  }
  return blocks.filter((block) => block.kind !== 'text' || block.lines.length > 0)
}

// **bold**, *emphasis* and `code` inside a line.
export function Inline({ text }: { text: string }) {
  return text.split(/(`[^`]+`|\*\*[^*]+\*\*|\*[^*\s][^*]*\*)/g).map((part, i) =>
    part.startsWith('`') && part.endsWith('`') && part.length > 2 ? (
      <code key={i} className="rounded bg-muted px-1 py-0.5 font-mono text-[0.9em]">
        {part.slice(1, -1)}
      </code>
    ) : part.startsWith('**') && part.endsWith('**') && part.length > 4 ? (
      <strong key={i} className="font-semibold">
        {part.slice(2, -2)}
      </strong>
    ) : part.startsWith('*') && part.endsWith('*') && part.length > 2 ? (
      <em key={i}>{part.slice(1, -1)}</em>
    ) : (
      <Fragment key={i}>{part}</Fragment>
    ),
  )
}

export function Markdown({ text }: { text: string }) {
  return (
    <div className="grid gap-3 text-pretty">
      {parse(text).map((block, i) =>
        block.kind === 'code' ? (
          <CodeBlock key={i} code={block.code} language={block.language} />
        ) : block.kind === 'list' ? (
          block.ordered ? (
            <ol key={i} start={block.start} className="grid list-decimal gap-1 pl-6">
              {block.items.map((entry, j) => (
                <li key={j}>
                  <Inline text={entry} />
                </li>
              ))}
            </ol>
          ) : (
            <ul key={i} className="grid list-disc gap-1 pl-6">
              {block.items.map((entry, j) => (
                <li key={j}>
                  <Inline text={entry} />
                </li>
              ))}
            </ul>
          )
        ) : (
          <p key={i}>
            {block.lines.map((line, j) => (
              <Fragment key={j}>
                {j > 0 && <br />}
                <Inline text={line} />
              </Fragment>
            ))}
          </p>
        ),
      )}
    </div>
  )
}
