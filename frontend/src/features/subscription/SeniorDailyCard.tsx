import { Check, Crown } from 'lucide-react'
import { useState } from 'react'
import coinSmall from '@/assets/brand/stackoff-coin-small-on-dark.svg'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { useAuth } from '@/features/auth/auth-context'
import { useAsync } from '@/hooks/use-async'
import { api } from '@/lib/api-client'
import { useLocale } from '@/lib/i18n/locale-context'

// A Senior developer's 25 coins a day (docs/premium-pretplata.md): one click on the dashboard, once a day.
// Rendered only for a Senior (the dashboard checks me.plan), so a Junior never asks for the subscription.
export function SeniorDailyCard() {
  const { t } = useLocale()
  const copy = t.subscription.daily
  const { setUser } = useAuth()
  const status = useAsync(api.subscription)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string>()
  if (!status.data) return null
  const { dailyCoins, dailyCoinsClaimed } = status.data

  async function claim() {
    setBusy(true)
    setError(undefined)
    try {
      setUser(await api.claimDailyCoins()) // the top bar shows the new balance at once
      status.reload()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : t.auth.errors.generic)
    } finally {
      setBusy(false)
    }
  }

  return (
    <section
      aria-labelledby="senior-daily"
      className="flex flex-wrap items-center gap-x-4 gap-y-3 rounded-xl bg-card p-4 ring-1 ring-warning/40"
    >
      <span className="grid size-10 shrink-0 place-items-center rounded-lg bg-warning/15 text-warning">
        <Crown aria-hidden="true" className="size-5" />
      </span>
      <div className="grid min-w-0 flex-1 basis-56 gap-0.5">
        <h2 id="senior-daily" className="font-medium">
          {copy.title(dailyCoins)}
        </h2>
        <p className="text-sm text-pretty text-muted-foreground">{dailyCoinsClaimed ? copy.claimed : copy.open}</p>
      </div>
      {dailyCoinsClaimed ? (
        <Badge variant="success">
          <Check aria-hidden="true" />
          {t.dashboard.today.done}
        </Badge>
      ) : (
        <Button aria-disabled={busy} onClick={() => !busy && claim()}>
          <img src={coinSmall} alt="" className="size-4" />
          {copy.claim(dailyCoins)}
        </Button>
      )}
      {error && (
        <p role="alert" className="basis-full text-sm text-destructive">
          {error}
        </p>
      )}
    </section>
  )
}
