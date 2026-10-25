import bash from 'highlight.js/lib/languages/bash'
import csharp from 'highlight.js/lib/languages/csharp'
import css from 'highlight.js/lib/languages/css'
import dockerfile from 'highlight.js/lib/languages/dockerfile'
import http from 'highlight.js/lib/languages/http'
import javascript from 'highlight.js/lib/languages/javascript'
import json from 'highlight.js/lib/languages/json'
import nginx from 'highlight.js/lib/languages/nginx'
import python from 'highlight.js/lib/languages/python'
import sql from 'highlight.js/lib/languages/sql'
import typescript from 'highlight.js/lib/languages/typescript'
import xml from 'highlight.js/lib/languages/xml'
import yaml from 'highlight.js/lib/languages/yaml'
import { createLowlight } from 'lowlight'

// Grammars for the `language` values in Seed/questions (js, jsx, ts, py, sh, sql, css, html, yaml, http, dockerfile,
// nginx) plus what AI lesson cards tend to send; aliases such as "js", "py" and "sh" come with each grammar.
const lowlight = createLowlight({ bash, csharp, css, dockerfile, http, javascript, json, nginx, python, sql, typescript, xml, yaml })

type Node = ReturnType<typeof lowlight.highlight>['children'][number]

export interface Token {
  text: string
  tone?: string // a text-code-* class; none = plain foreground
}

const tones: Record<string, string> = {
  'hljs-keyword': 'text-code-keyword',
  'hljs-selector-tag': 'text-code-keyword',
  'hljs-section': 'text-code-keyword',
  'hljs-doctag': 'text-code-keyword',
  'hljs-string': 'text-code-string',
  'hljs-char': 'text-code-string',
  'hljs-regexp': 'text-code-string',
  'hljs-addition': 'text-code-string',
  'hljs-number': 'text-code-number',
  'hljs-literal': 'text-code-number',
  'hljs-symbol': 'text-code-number',
  'hljs-bullet': 'text-code-number',
  'hljs-comment': 'text-code-comment italic',
  'hljs-quote': 'text-code-comment italic',
  'hljs-title': 'text-code-function',
  'hljs-built_in': 'text-code-type',
  'hljs-type': 'text-code-type',
  'hljs-attr': 'text-code-attr',
  'hljs-attribute': 'text-code-attr',
  'hljs-property': 'text-code-attr',
  'hljs-selector-class': 'text-code-attr',
  'hljs-selector-id': 'text-code-attr',
  'hljs-selector-attr': 'text-code-attr',
  'hljs-selector-pseudo': 'text-code-attr',
  'hljs-name': 'text-code-tag',
  'hljs-variable': 'text-code-tag',
  'hljs-template-variable': 'text-code-tag',
  'hljs-deletion': 'text-code-tag',
  'hljs-meta': 'text-code-meta',
  'hljs-subst': 'text-foreground', // ${…} inside a template string reads as code again
}

const toneOf = (className: unknown) =>
  Array.isArray(className) ? className.map((name) => tones[String(name)]).find(Boolean) : undefined

// Splits code into lines of colored tokens, so CodeBlock keeps its numbered rows. Unknown or missing languages
// ("text", null) come back as plain lines.
export function highlightLines(code: string, language?: string | null): Token[][] {
  const name = language?.toLowerCase()
  if (!name || !lowlight.registered(name)) return code.split('\n').map((text) => [{ text }])

  let line: Token[] = []
  const lines = [line]
  const walk = (nodes: Node[], tone?: string) => {
    for (const node of nodes) {
      if (node.type === 'text') {
        node.value.split('\n').forEach((text, index) => {
          if (index > 0) lines.push((line = []))
          if (text) line.push({ text, tone })
        })
      } else if (node.type === 'element') {
        walk(node.children, toneOf(node.properties.className) ?? tone)
      }
    }
  }
  walk(lowlight.highlight(name, code).children)
  return lines
}
