import { Check, Snowflake } from 'lucide-react'
import { useEffect, useState } from 'react'
import coinSmall from '@/assets/brand/stackoff-coin-small-on-dark.svg'
import { Rewards } from '@/components/shared/Rewards'
import { UserAvatar } from '@/components/shared/UserAvatar'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useAuth } from '@/features/auth/auth-context'
import { useAsync } from '@/hooks/use-async'
import { api, ApiError } from '@/lib/api-client'
import { avatarOf } from '@/lib/avatars'
import { bannerClass, boardClass, cardClass, nameClass, SLOTS } from '@/lib/cosmetics'
import { relativeTime } from '@/lib/format'
import type { Messages } from '@/lib/i18n/en'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import type { MeDto, ShopItemDto, ShopItemKind, UserRefDto } from '@/types/api'

const KIND_ORDER: ShopItemKind[] = ['avatar_frame', 'name_color', 'banner', 'card_background', 'board_theme', 'streak_freeze']
const MAX_FREEZES = 2 // GameRules: at most two streak freezes held

const errorText = (caught: unknown, t: Messages) => {
  if (!(caught instanceof ApiError)) return t.auth.errors.generic
  const errors = t.shop.errors
  switch (caught.code) {
    case 'urn:app:shop/insufficient-coins':
      return errors.insufficient
    case 'urn:app:shop/already-owned':
      return errors.owned
    case 'urn:app:shop/freeze-limit':
      return errors.freezeLimit
    case 'urn:app:shop/not-owned':
      return errors.notOwned
  }
  return caught.status === 0 ? t.common.serverUnreachable : caught.message
}

// Coins buy looks only (game idea §13): frames, name colors, banners, card backgrounds, duel boards, streak freezes.
export function ShopPage() {
  const { t, locale } = useLocale()
  const { session, refreshUser } = useAuth()
  const shop = useAsync(api.shop)

  // The balance in the top bar can be a duel behind; read it fresh once.
  useEffect(() => {
    refreshUser().catch(() => {})
  }, [refreshUser])

  if (session.status !== 'authenticated') return null
  const me = session.user

  return (
    <main className="mx-auto grid w-full max-w-7xl content-start gap-5 px-4 pb-10 sm:px-6 lg:px-8">
      <title>{`${t.shop.documentTitle} · StackOff`}</title>
      <header className="flex flex-wrap items-end gap-3">
        <div className="grid flex-1 gap-1">
          <h1 className="font-heading text-2xl font-semibold tracking-tight sm:text-3xl">{t.shop.title}</h1>
          <p className="text-sm text-pretty text-muted-foreground">{t.shop.subtitle}</p>
        </div>
        <p className="flex items-center gap-2 rounded-lg bg-card px-3 py-2 font-mono text-lg font-medium ring-1 ring-border">
          <img src={coinSmall} alt="" className="size-5" />
          <span className="sr-only">{t.shell.coins}:</span>
          {me.coins.toLocaleString(locale)}
        </p>
      </header>
      {shop.data ? (
        KIND_ORDER.map((kind) => {
          const items = shop.data!.items.filter((item) => item.kind === kind)
          return (
            items.length > 0 && (
              <section key={kind} className="grid gap-3">
                <h2 className="font-heading text-lg font-semibold">{t.shop.kinds[kind]}</h2>
                <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
                  {items.map((item) => (
                    <ShopItem key={item.id} item={item} me={me} onBought={shop.reload} />
                  ))}
                </ul>
              </section>
            )
          )
        })
      ) : shop.error ? (
        <Card role="alert">
          <CardContent className="grid justify-items-start gap-3">
            <p>{t.shop.loadFailed}</p>
            <p className="text-sm text-muted-foreground">{shop.error.message}</p>
            <Button variant="outline" onClick={shop.reload}>
              {t.common.retry}
            </Button>
          </CardContent>
        </Card>
      ) : (
        <div aria-busy="true" className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          <p role="status" className="sr-only">
            {t.common.loading}
          </p>
          {Array.from({ length: 4 }, (_, index) => (
            <Skeleton key={index} className="h-56 rounded-xl" />
          ))}
        </div>
      )}
      <RewardHistory />
    </main>
  )
}

function ShopItem({ item, me, onBought }: { item: ShopItemDto; me: MeDto; onBought: () => void }) {
  const { t } = useLocale()
  const copy = t.shop
  const { setUser } = useAuth()
  const [confirming, setConfirming] = useState(false)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string>()
  const slot = SLOTS[item.kind]
  const equipped = !!slot && me.equipment[slot] === item.assetKey
  const freeze = item.kind === 'streak_freeze'
  const short = item.price - me.coins

  useEffect(() => {
    if (!confirming) return
    const id = window.setTimeout(() => setConfirming(false), 3000)
    return () => window.clearTimeout(id)
  }, [confirming])

  async function run(command: () => Promise<MeDto>, after?: () => void) {
    setBusy(true)
    setError(undefined)
    try {
      setUser(await command())
      after?.()
    } catch (caught) {
      setError(errorText(caught, t))
    } finally {
      setBusy(false)
      setConfirming(false)
    }
  }

  const buy = () => (confirming ? run(() => api.purchase({ itemId: item.id }), onBought) : setConfirming(true))
  const action =
    item.owned && slot ? (
      <Button
        variant={equipped ? 'outline' : 'default'}
        aria-disabled={busy}
        onClick={() => !busy && run(() => api.equip({ [slot]: equipped ? null : item.id }))}
      >
        {equipped ? copy.unequip : copy.equip}
      </Button>
    ) : freeze && me.streak.freezes >= MAX_FREEZES ? (
      <p className="text-sm text-muted-foreground">{copy.freezeMax}</p>
    ) : short > 0 ? (
      <p className="text-sm text-muted-foreground">{copy.needMore(short)}</p>
    ) : (
      <Button variant={confirming ? 'default' : 'outline'} aria-disabled={busy} onClick={() => !busy && buy()}>
        <img src={coinSmall} alt="" className="size-4" />
        {confirming ? copy.confirm(item.price) : copy.buy(item.price)}
      </Button>
    )

  return (
    <li>
      <Card className="h-full">
        <CardContent className="grid h-full content-between gap-4">
          <div className="grid gap-3">
            <div className="grid h-28 place-items-center overflow-hidden rounded-lg bg-secondary ring-1 ring-border ring-inset">
              <Preview item={item} me={me} />
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <h3 className="font-medium">{item.name}</h3>
              <Badge variant={item.rarity === 'common' ? 'secondary' : 'outline'}>{copy.rarity[item.rarity as keyof typeof copy.rarity] ?? item.rarity}</Badge>
              {equipped && (
                <Badge variant="success">
                  <Check aria-hidden="true" />
                  {copy.equipped}
                </Badge>
              )}
            </div>
            {freeze && <p className="text-sm text-muted-foreground">{copy.freezes(me.streak.freezes, MAX_FREEZES)}</p>}
          </div>
          <div className="grid justify-items-start gap-2">
            {action}
            {error && (
              <p role="alert" className="text-sm text-destructive">
                {error}
              </p>
            )}
          </div>
        </CardContent>
      </Card>
    </li>
  )
}

// The item on me: my portrait in the frame, my name in the color, the pattern itself.
function Preview({ item, me }: { item: ShopItemDto; me: MeDto }) {
  const { t } = useLocale()
  const slot = SLOTS[item.kind]
  const on: Pick<UserRefDto, 'equipment'> = { equipment: slot ? { [slot]: item.assetKey } : {} }

  switch (item.kind) {
    case 'avatar_frame':
      return <UserAvatar user={{ id: `preview-${item.id}`, avatar: avatarOf(me).key, ...on }} className="size-16" />
    case 'name_color':
      return <span className={cn('font-heading text-xl font-semibold', nameClass(on))}>{me.username}</span>
    case 'banner':
      return (
        <span className={cn('flex size-full items-end p-3 font-mono text-sm text-success', bannerClass(on))}>
          ~/{me.username} $<span className="ml-1 inline-block h-4 w-2 bg-success" />
        </span>
      )
    case 'card_background':
      return <span className={cn('size-full', cardClass(on))} />
    case 'board_theme':
      return (
        <span className={cn('flex size-full items-center justify-center gap-6', boardClass(on))}>
          <UserAvatar user={me} className="size-10" />
          <span className="font-mono text-xs text-muted-foreground">vs</span>
          <span className="size-10 rounded-full bg-secondary ring-1 ring-border" />
        </span>
      )
    default:
      return (
        <span className="grid justify-items-center gap-1 text-primary">
          <Snowflake aria-hidden="true" className="size-10" />
          <span className="sr-only">{t.shop.kinds.streak_freeze}</span>
        </span>
      )
  }
}

// Where coins and XP came from: GET /api/me/rewards (newest first, purchases negative).
function RewardHistory() {
  const { t, locale } = useLocale()
  const copy = t.shop.history
  const rewards = useAsync(api.rewards)

  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <h2>{copy.title}</h2>
        </CardTitle>
        <CardDescription>{copy.subtitle}</CardDescription>
      </CardHeader>
      <CardContent>
        {rewards.data ? (
          rewards.data.length ? (
            <ul className="grid max-h-96 gap-1.5 overflow-y-auto [scrollbar-width:thin]">
              {rewards.data.map((reward, index) => (
                <li key={`${reward.at}-${index}`} className="flex flex-wrap items-center gap-x-3 gap-y-1 rounded-lg px-3 py-2 ring-1 ring-border ring-inset">
                  <span className="min-w-0 flex-1 text-sm font-medium">{copy.types[reward.type] ?? reward.type}</span>
                  <Rewards coins={reward.coins} xp={reward.xp} sp={reward.sp} className="justify-end [&_li]:px-2 [&_li]:py-0.5 [&_li]:text-xs" />
                  <span className="w-28 text-right font-mono text-xs text-muted-foreground">{relativeTime(reward.at, locale)}</span>
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-sm text-muted-foreground">{copy.empty}</p>
          )
        ) : rewards.error ? (
          <p role="alert" className="text-sm text-destructive">
            {rewards.error.message}
          </p>
        ) : (
          <Skeleton aria-busy="true" className="h-40 rounded-lg" />
        )}
      </CardContent>
    </Card>
  )
}
