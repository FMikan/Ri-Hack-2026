import { Brain, Braces, BookOpen, Database, GitBranch, Globe, LayoutTemplate, Server, ShieldCheck, type LucideIcon } from 'lucide-react'

// Keyed by topic slug (game idea §3). Unknown slugs get a book until they are added here.
const ICONS: Record<string, LucideIcon> = {
  javascript: Braces,
  frontend: LayoutTemplate,
  backend: Server,
  sql: Database,
  networking: Globe,
  security: ShieldCheck,
  algorithms: GitBranch,
  'how-ai-works': Brain,
}

export const topicIcon = (slug: string) => ICONS[slug] ?? BookOpen
