import { Binary, Braces, BookOpen, Cloud, Database, FlaskConical, GitBranch, Globe, LayoutTemplate, Network, Server, ShieldCheck, Terminal, type LucideIcon } from 'lucide-react'

// Keyed by topic slug (game idea §3). Unknown slugs get a book until they are added here.
const ICONS: Record<string, LucideIcon> = {
  javascript: Braces,
  frontend: LayoutTemplate,
  backend: Server,
  sql: Database,
  networking: Globe,
  security: ShieldCheck,
  algorithms: Binary,
  git: GitBranch,
  linux: Terminal,
  testing: FlaskConical,
  devops: Cloud,
  'system-design': Network,
}

export const topicIcon = (slug: string) => ICONS[slug] ?? BookOpen
