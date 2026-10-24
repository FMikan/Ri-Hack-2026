import wordmark from '@/assets/brand/stackoff-wordmark-on-dark.svg'
import { cn } from '@/lib/utils'

// Brand kit rules: at least 120px wide (h-6 or taller), never recoloured; this file is for navy and slate surfaces.
export function Wordmark({ className }: { className?: string }) {
  return <img src={wordmark} alt="StackOff" className={cn('h-7 w-auto', className)} />
}
