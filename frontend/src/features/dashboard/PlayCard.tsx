import { ArrowRight, Flame, Lock, type LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { BotAvatar } from '@/components/shared/BotAvatar'
import { DuelBadge } from '@/components/shared/RankedDuelIcon'
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
          icon={Lock}
          media={locked ? undefined : <DuelBadge className="size-12" />}
          label={copy.ranked}
          hint={locked ? copy.lockedHint : copy.rankedHint}
          locked={locked}
          to={locked ? undefined : '?play=ranked'}
          primary
        />
        {me.activeMatchId ? (
          <PlayOption icon={Lock} media={<DuelBadge className="size-12" />} label={copy.resume} hint={copy.resumeHint} locked={false} to={`/match/${me.activeMatchId}`} />
        ) : (
          <PlayOption
            icon={Lock}
            media={locked ? undefined : <BotAvatar persona={bot.persona} className="size-12" />}
            label={copy.bot}
            hint={locked ? copy.lockedHint : copy.botHint(t.bots[bot.persona], bot.rating)}
            locked={locked}
            to={locked ? undefined : '?play=bot'}
          />
        )}
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
  media?: ReactNode // replaces the icon, e.g. the suggested bot's portrait
  to?: string // playable now; without it the tile is a placeholder that says "Soon"
}

// Ranked is the main action: sky ring, tint and filled icon at rest, solid sky on hover (dark text on sky, 7:1). The
// other tiles rest neutral and take the tint and ring on hover. Keyboard focus looks like hover; the arrow nudges right.
const STYLES = {
  primary: {
    tile: 'bg-primary/10 ring-primary hover:bg-primary hover:text-primary-foreground focus-visible:bg-primary focus-visible:text-primary-foreground',
    icon: 'bg-primary text-primary-foreground group-hover/play:bg-primary-foreground group-hover/play:text-primary group-focus-visible/play:bg-primary-foreground group-focus-visible/play:text-primary',
    hint: 'group-hover/play:text-primary-foreground/75 group-focus-visible/play:text-primary-foreground/75',
    arrow: 'text-primary group-hover/play:text-primary-foreground group-focus-visible/play:text-primary-foreground',
  },
  secondary: {
    tile: 'bg-secondary ring-border hover:bg-primary/10 hover:ring-primary focus-visible:bg-primary/10 focus-visible:ring-primary',
    icon: 'bg-primary/15 text-primary group-hover/play:bg-primary group-hover/play:text-primary-foreground group-focus-visible/play:bg-primary group-focus-visible/play:text-primary-foreground',
    hint: '',
    arrow: 'text-muted-foreground group-hover/play:text-primary group-focus-visible/play:text-primary',
  },
}
const motion = 'duration-200 ease-out motion-reduce:transition-none'

// Both tiles open the play dialog: the bot picker, or the ranked queue (a local stand-in until F3).
function PlayOption({ icon: Icon, label, hint, locked, primary, media, to }: PlayOptionProps) {
  const { t } = useLocale()
  const style = STYLES[primary ? 'primary' : 'secondary']
  const className = cn(
    'flex min-h-24 items-center gap-4 rounded-lg p-4 text-left text-foreground ring-1 ring-inset focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring sm:p-5',
    to
      ? cn('group/play transition-[background-color,color,box-shadow,scale] active:scale-[0.98]', motion, style.tile)
      : 'cursor-not-allowed bg-secondary ring-border',
  )
  const body = (
    <>
      {media ?? (
        <span
          className={cn(
            'grid size-12 shrink-0 place-items-center rounded-full transition-colors',
            motion,
            to ? style.icon : 'bg-primary/15 text-primary',
          )}
        >
          <Icon aria-hidden="true" className="size-6" />
        </span>
      )}
      <span className="grid min-w-0 gap-0.5">
        <span className="font-heading text-lg leading-tight font-semibold sm:text-xl">{label}</span>
        <span className={cn('text-sm text-pretty text-muted-foreground transition-colors', motion, to && style.hint)}>{hint}</span>
      </span>
      {to ? (
        <ArrowRight
          aria-hidden="true"
          className={cn(
            'ml-auto size-5 shrink-0 transition-[color,translate] group-hover/play:translate-x-1 group-focus-visible/play:translate-x-1',
            motion,
            style.arrow,
          )}
        />
      ) : (
        !locked && <span className="ml-auto shrink-0 self-start font-mono text-xs opacity-75">{t.shell.soon}</span>
      )}
    </>
  )

  // ?play=… opens the play dialog over the dashboard without adding a history entry.
  return to ? (
    <Link to={to} replace={to.startsWith('?')} className={className}>
      {body}
    </Link>
  ) : (
    <button type="button" aria-disabled="true" className={className}>
      {body}
    </button>
  )
}
