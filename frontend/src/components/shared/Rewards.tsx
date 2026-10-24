import coinSmall from '@/assets/brand/stackoff-coin-small-on-dark.svg'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'

interface RewardsProps {
  coins: number
  xp: number
  sp?: number
  className?: string
}

// What a match, lesson or challenge paid: "+50 coins +100 XP +2 SP". Zero amounts are left out.
export function Rewards({ coins, xp, sp = 0, className }: RewardsProps) {
  const { t } = useLocale()
  const chip = 'inline-flex items-center gap-1.5 rounded-full bg-secondary px-3 py-1 font-mono text-sm font-medium'
  if (!coins && !xp && !sp) return null

  return (
    <ul aria-label={t.rewards.label} className={cn('flex flex-wrap justify-center gap-2', className)}>
      {coins !== 0 && (
        <li className={chip}>
          <img src={coinSmall} alt="" className="size-4" />
          {t.rewards.coins(coins)}
        </li>
      )}
      {xp !== 0 && <li className={cn(chip, 'text-primary')}>{t.rewards.xp(xp)}</li>}
      {sp !== 0 && <li className={cn(chip, 'text-success')}>{t.rewards.sp(sp)}</li>}
    </ul>
  )
}
