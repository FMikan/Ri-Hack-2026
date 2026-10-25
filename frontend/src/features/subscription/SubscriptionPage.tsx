import { ArrowLeft, Check, Crown, CreditCard, Minus } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Link } from 'react-router'
import { SegmentedControl } from '@/components/shared/SegmentedControl'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useAsync } from '@/hooks/use-async'
import { api } from '@/lib/api-client'
import { euros, longDate } from '@/lib/format'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import type { PremiumPlanDto, SubscriptionDto } from '@/types/api'
import { PayDialog } from './PayDialog'

type Interval = PremiumPlanDto['interval']

interface PerkRow {
  label: string
  value: string
  included: boolean
}

// The two plans side by side (docs/premium-pretplata.md): Junior developer is free, Senior developer is the paid one.
// The payment is simulated for the hackathon: PayDialog never charges a card.
export function SubscriptionPage() {
  const { t, locale } = useLocale()
  const copy = t.subscription
  const subscription = useAsync(api.subscription)
  const [interval, setInterval] = useState<Interval>('yearly')
  const [paying, setPaying] = useState(false)
  const plans = subscription.data?.plans
  const plan = plans?.find((option) => option.interval === interval)
  const monthly = plans?.find((option) => option.interval === 'monthly')
  const yearly = plans?.find((option) => option.interval === 'yearly')
  const saving = monthly && yearly ? Math.round(100 - (100 * yearly.priceCents) / (monthly.priceCents * 12)) : 0

  const perks = copy.perks
  const rows = (senior: boolean): PerkRow[] => [
    { label: perks.rating, value: senior ? perks.ratingSenior : perks.ratingJunior, included: senior },
    { label: perks.xp, value: senior ? perks.xpSenior : perks.xpJunior, included: senior },
    { label: perks.professor, value: senior ? perks.professorSenior : perks.professorJunior, included: true },
    { label: perks.coins, value: senior ? perks.coinsSenior : perks.coinsJunior, included: senior },
    { label: perks.pack, value: senior ? perks.packSenior : perks.packJunior, included: senior },
    { label: perks.badge, value: senior ? perks.badgeSenior : perks.badgeJunior, included: senior },
  ]

  return (
    <main className="mx-auto grid w-full max-w-5xl content-start gap-6 px-4 pb-10 sm:px-6 lg:px-8">
      <title>{`${copy.documentTitle} · StackOff`}</title>
      <header className="grid justify-items-start gap-2">
        <Link
          to="/"
          className="flex items-center gap-1.5 rounded-sm text-sm text-muted-foreground hover:text-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        >
          <ArrowLeft aria-hidden="true" className="size-4" />
          {copy.backToDashboard}
        </Link>
        <h1 className="font-heading text-2xl font-semibold tracking-tight sm:text-3xl">{copy.title}</h1>
        <p className="max-w-prose text-sm text-pretty text-muted-foreground">{copy.subtitle}</p>
      </header>
      {subscription.data ? (
        <>
          {subscription.data.plan === 'senior' && (
            <SeniorStatus status={subscription.data} onChanged={subscription.reload} onExtend={() => setPaying(true)} />
          )}
          <div className="flex flex-wrap items-center gap-3">
            <SegmentedControl
              label={copy.billing}
              name="billing"
              value={interval}
              onChange={setInterval}
              toggle
              className="w-64"
              options={[
                { value: 'monthly', label: copy.monthly },
                { value: 'yearly', label: copy.yearly },
              ]}
            />
            {saving > 0 && <Badge variant="success">{copy.save(saving)}</Badge>}
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <PlanCard
              name={copy.junior}
              price={copy.free}
              current={subscription.data.plan === 'junior'}
              rows={rows(false)}
            />
            <PlanCard
              senior
              name={copy.senior}
              price={plan ? euros(plan.priceCents, locale) : ''}
              per={interval === 'monthly' ? copy.perMonth : copy.perYear}
              note={interval === 'yearly' && yearly ? copy.yearlyNote(euros(Math.round(yearly.priceCents / 12), locale)) : undefined}
              current={subscription.data.plan === 'senior'}
              rows={rows(true)}
              action={
                <Button size="lg" className="w-full sm:w-auto" onClick={() => setPaying(true)}>
                  <CreditCard aria-hidden="true" />
                  {subscription.data.plan === 'senior' ? copy.status.extend : copy.pay}
                </Button>
              }
            />
          </div>
          <p className="text-xs text-pretty text-muted-foreground">{copy.demoNote}</p>
          {plan && <PayDialog open={paying} onOpenChange={setPaying} plan={plan} onPaid={subscription.reload} />}
        </>
      ) : subscription.error ? (
        <Card role="alert">
          <CardContent className="grid justify-items-start gap-3">
            <p>{copy.loadFailed}</p>
            <p className="text-sm text-muted-foreground">{subscription.error.message}</p>
            <Button variant="outline" onClick={subscription.reload}>
              {t.common.retry}
            </Button>
          </CardContent>
        </Card>
      ) : (
        <div aria-busy="true" className="grid gap-4 sm:grid-cols-2">
          <p role="status" className="sr-only">
            {t.common.loading}
          </p>
          <Skeleton className="h-96 rounded-xl" />
          <Skeleton className="h-96 rounded-xl" />
        </div>
      )}
    </main>
  )
}

interface PlanCardProps {
  name: string
  price: string
  per?: string
  note?: string
  current: boolean
  senior?: boolean
  rows: PerkRow[]
  action?: ReactNode
}

function PlanCard({ name, price, per, note, current, senior, rows, action }: PlanCardProps) {
  const { t } = useLocale()

  return (
    <Card className={cn('h-full', senior && 'bg-[color-mix(in_oklch,var(--card),var(--warning)_6%)] ring-warning/40')}>
      <CardContent className="grid h-full content-start gap-5">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="grid gap-1">
            <h2 className="flex items-center gap-2 font-heading text-xl font-semibold">
              {senior && <Crown aria-hidden="true" className="size-5 text-warning" />}
              {name}
            </h2>
            <p className="font-heading text-3xl font-semibold tabular-nums">
              {price}
              {per && <span className="text-base font-normal text-muted-foreground"> {per}</span>}
            </p>
            {note && <p className="text-xs text-muted-foreground">{note}</p>}
          </div>
          {current && <Badge variant="secondary">{t.subscription.yourPlan}</Badge>}
        </div>
        <ul className="grid gap-3">
          {rows.map((row) => (
            <li key={row.label} className="grid grid-cols-[auto_minmax(0,1fr)] gap-3 text-sm">
              {row.included ? (
                <Check aria-hidden="true" className="mt-0.5 size-4 text-success" />
              ) : (
                <Minus aria-hidden="true" className="mt-0.5 size-4 text-muted-foreground" />
              )}
              <span className="grid gap-0.5">
                <span className="font-medium">{row.label}</span>
                <span className="text-pretty text-muted-foreground">{row.value}</span>
              </span>
            </li>
          ))}
        </ul>
        {action && <div className="mt-auto pt-2">{action}</div>}
      </CardContent>
    </Card>
  )
}

interface SeniorStatusProps {
  status: SubscriptionDto
  onChanged: () => void
  onExtend: () => void
}

// Until when, whether it renews, which card; cancelling only turns the renewal off (DELETE /api/subscription).
function SeniorStatus({ status, onChanged, onExtend }: SeniorStatusProps) {
  const { t, locale } = useLocale()
  const copy = t.subscription.status
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string>()
  const until = status.premiumUntil ? longDate(status.premiumUntil, locale) : ''
  const interval = status.interval === 'yearly' ? t.subscription.yearly : t.subscription.monthly

  async function cancel() {
    setBusy(true)
    setError(undefined)
    try {
      await api.cancelSubscription()
      onChanged()
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : t.auth.errors.generic)
    } finally {
      setBusy(false)
    }
  }

  return (
    <section
      aria-labelledby="senior-status"
      className="flex flex-wrap items-center gap-x-4 gap-y-3 rounded-xl bg-card p-4 ring-1 ring-warning/40"
    >
      <span className="grid size-10 shrink-0 place-items-center rounded-lg bg-warning/15 text-warning">
        <Crown aria-hidden="true" className="size-5" />
      </span>
      <div className="grid min-w-0 flex-1 basis-56 gap-0.5">
        <h2 id="senior-status" className="font-medium">
          {copy.title}
        </h2>
        <p className="text-sm text-pretty text-muted-foreground">
          {status.autoRenew ? copy.renews(until, interval.toLowerCase()) : copy.ends(until)}
          {status.cardLast4 && ` · ${copy.card(status.cardLast4)}`}
        </p>
      </div>
      <div className="flex items-center gap-2">
        <Button variant="outline" onClick={onExtend}>
          {copy.extend}
        </Button>
        {status.autoRenew && (
          <Button variant="ghost" aria-disabled={busy} onClick={() => !busy && cancel()}>
            {copy.cancel}
          </Button>
        )}
      </div>
      {error && (
        <p role="alert" className="basis-full text-sm text-destructive">
          {error}
        </p>
      )}
    </section>
  )
}
