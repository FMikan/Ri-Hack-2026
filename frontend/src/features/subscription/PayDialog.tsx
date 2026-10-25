import { Check, CreditCard, WandSparkles } from 'lucide-react'
import { useState, type ChangeEvent, type FormEvent } from 'react'
import { Link } from 'react-router'
import { Button } from '@/components/ui/button'
import { Dialog, DialogContent } from '@/components/ui/dialog'
import { useAuth } from '@/features/auth/auth-context'
import { TextField } from '@/features/auth/TextField'
import { dialogFooter } from '@/features/play/dialog-footer'
import { DialogHeading } from '@/features/play/dialog-parts'
import { api, ApiError } from '@/lib/api-client'
import { euros, longDate } from '@/lib/format'
import { useLocale } from '@/lib/i18n/locale-context'
import type { PremiumPlanDto, SubscriptionDto } from '@/types/api'

// Stands in for the card form a payment provider would host (Stripe Elements in production). Nothing is charged and
// the API keeps only the last four digits. "Use this card" fills in a test card, so the demo takes one click.
const DEMO_CARD = { number: '4242 4242 4242 4242', expiry: '12/28', cvc: '123' }

interface PayDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  plan: PremiumPlanDto
  onPaid: () => void
}

export function PayDialog({ open, onOpenChange, plan, onPaid }: PayDialogProps) {
  const { t, locale } = useLocale()
  const copy = t.subscription.dialog
  const { session, refreshUser } = useAuth()
  const username = session.status === 'authenticated' ? session.user.username : ''
  const [card, setCard] = useState({ holder: '', number: '', expiry: '', cvc: '' })
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string>()
  const [paid, setPaid] = useState<SubscriptionDto>()
  const price = euros(plan.priceCents, locale)
  const set = (key: keyof typeof card) => (event: ChangeEvent<HTMLInputElement>) =>
    setCard((previous) => ({ ...previous, [key]: event.target.value }))

  function close(next: boolean) {
    if (busy) return
    onOpenChange(next)
    if (!next) {
      setError(undefined)
      setPaid(undefined)
    }
  }

  async function pay(event: FormEvent) {
    event.preventDefault()
    if (busy) return
    setBusy(true)
    setError(undefined)
    try {
      const result = await api.subscribe({
        interval: plan.interval,
        cardHolder: card.holder,
        cardNumber: card.number,
        cardExpiry: card.expiry,
        cardCvc: card.cvc,
      })
      setPaid(result)
      await refreshUser().catch(() => {}) // the plan and the welcome pack show at once
      onPaid()
    } catch (caught) {
      setError(
        caught instanceof ApiError
          ? caught.code === 'urn:app:payment/declined'
            ? copy.declined
            : caught.status === 0
              ? t.common.serverUnreachable
              : caught.message
          : t.auth.errors.generic,
      )
    } finally {
      setBusy(false)
    }
  }

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent showCloseButton={false} className="gap-5 bg-card p-5 sm:max-w-md sm:p-6">
        {paid ? (
          <>
            <DialogHeading title={copy.paidTitle} description={copy.paidBody(paid.premiumUntil ? longDate(paid.premiumUntil, locale) : '')} />
            <ul className="grid gap-2 text-sm">
              {copy.paidPerks.map((perk) => (
                <li key={perk} className="flex gap-2">
                  <Check aria-hidden="true" className="mt-0.5 size-4 shrink-0 text-success" />
                  {perk}
                </li>
              ))}
            </ul>
            <div className={dialogFooter}>
              <Button asChild variant="outline">
                <Link to="/profile">{copy.openLocker}</Link>
              </Button>
              <Button onClick={() => close(false)}>{copy.done}</Button>
            </div>
          </>
        ) : (
          <form onSubmit={pay} className="contents">
            <DialogHeading
              title={copy.title(t.subscription.senior)}
              description={copy.body(price, plan.interval === 'monthly' ? t.subscription.perMonth : t.subscription.perYear)}
            />
            <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg bg-secondary px-3 py-2.5 text-xs ring-1 ring-border ring-inset">
              <span className="text-muted-foreground">{copy.demoHint}</span>
              <Button type="button" variant="outline" size="sm" onClick={() => setCard({ holder: username, ...DEMO_CARD })}>
                <WandSparkles aria-hidden="true" />
                {copy.useThisCard}
              </Button>
            </div>
            <div className="grid gap-4">
              <TextField label={copy.holder} value={card.holder} onChange={set('holder')} autoComplete="cc-name" required />
              <TextField
                label={copy.number}
                value={card.number}
                onChange={set('number')}
                inputMode="numeric"
                autoComplete="cc-number"
                placeholder="4242 4242 4242 4242"
                required
              />
              <div className="grid grid-cols-2 gap-4">
                <TextField label={copy.expiry} value={card.expiry} onChange={set('expiry')} inputMode="numeric" autoComplete="cc-exp" placeholder="MM/YY" required />
                <TextField label={copy.cvc} value={card.cvc} onChange={set('cvc')} inputMode="numeric" autoComplete="cc-csc" placeholder="123" required />
              </div>
            </div>
            {error && (
              <p role="alert" className="text-sm text-destructive">
                {error}
              </p>
            )}
            <div className={dialogFooter}>
              <Button type="button" variant="ghost" onClick={() => close(false)}>
                {t.common.close}
              </Button>
              <Button type="submit" aria-disabled={busy}>
                <CreditCard aria-hidden="true" />
                {busy ? copy.paying : copy.pay(price)}
              </Button>
            </div>
          </form>
        )}
      </DialogContent>
    </Dialog>
  )
}
