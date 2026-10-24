import { Bot, Flame, Lock, Swords, type LucideIcon } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { recommendedBot } from '@/lib/game'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import type { MeDto } from '@/types/api'

// The point of the game, first card on the page: ranked duel and bot duel. Locked until 3 topics are picked.
export function PlayCard({ me, locked, className }: { me: MeDto; locked: boolean; className?: string }) {
  const { t } = useLocale()
  const copy = t.dashboard.play
  const bot = recommendedBot(me.rating)
  const { count, securedToday } = me.streak

  return (
    <Card className={className}>
      <CardHeader>
        <CardTitle>
          <h2 className="text-2xl tracking-tight sm:text-3xl">{copy.title}</h2>
        </CardTitle>
        <CardDescription>{locked ? copy.locked : copy.body}</CardDescription>
        <CardAction>
          <Badge variant={securedToday ? 'success' : 'outline'}>
            <Flame aria-hidden="true" />
            {securedToday ? copy.streak(count) : count > 0 ? copy.keepStreak : copy.startStreak}
          </Badge>
        </CardAction>
      </CardHeader>
      <CardContent className="mt-auto grid gap-3 sm:grid-cols-2">
        <PlayOption
          icon={locked ? Lock : Swords}
          label={copy.ranked}
          hint={locked ? copy.lockedHint : copy.rankedHint}
          locked={locked}
          primary
        />
        <PlayOption
          icon={locked ? Lock : Bot}
          label={copy.bot}
          hint={locked ? copy.lockedHint : copy.botHint(t.dashboard.bots[bot.key], bot.rating)}
          locked={locked}
        />
        {locked && (
          <Button asChild variant="outline" className="justify-self-start">
            <a href="#topics">{copy.pickTopics}</a>
          </Button>
        )}
      </CardContent>
    </Card>
  )
}

interface PlayOptionProps {
  icon: LucideIcon
  label: string
  hint: string
  locked: boolean
  primary?: boolean
}

// Bot duel lands in F1 and ranked in F3; until then the tiles show where play starts and say "Soon".
function PlayOption({ icon: Icon, label, hint, locked, primary }: PlayOptionProps) {
  const { t } = useLocale()

  return (
    <button
      type="button"
      aria-disabled="true"
      className={cn(
        'flex min-h-24 cursor-not-allowed items-center gap-4 rounded-lg p-4 text-left focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring sm:p-5',
        primary ? 'bg-primary text-primary-foreground' : 'bg-secondary text-foreground ring-1 ring-border ring-inset',
      )}
    >
      <span
        className={cn(
          'grid size-12 shrink-0 place-items-center rounded-full',
          primary ? 'bg-primary-foreground text-primary' : 'bg-primary/15 text-primary',
        )}
      >
        <Icon aria-hidden="true" className="size-6" />
      </span>
      <span className="grid min-w-0 gap-0.5">
        <span className="font-heading text-lg leading-tight font-semibold sm:text-xl">{label}</span>
        <span className={cn('text-sm text-pretty', primary ? 'text-primary-foreground/75' : 'text-muted-foreground')}>
          {hint}
        </span>
      </span>
      {!locked && <span className="ml-auto shrink-0 self-start font-mono text-xs opacity-75">{t.shell.soon}</span>}
    </button>
  )
}
