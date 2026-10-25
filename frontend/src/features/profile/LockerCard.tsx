import { Check } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useAuth } from '@/features/auth/auth-context'
import { ItemPreview, RarityBadge } from '@/features/shop/ItemPreview'
import { SegmentedControl } from '@/components/shared/SegmentedControl'
import { useAsync } from '@/hooks/use-async'
import { api, ApiError } from '@/lib/api-client'
import { byKind, SLOTS } from '@/lib/cosmetics'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import type { EquipmentSlot, MeDto, ShopItemDto, ShopItemKind } from '@/types/api'

// My locker (GET /api/me/items): every cosmetic I bought or earned, one section per slot. One item per slot is worn;
// PUT /api/me/equipment swaps it and answers with the MeDto that every page (and the card above) reads from.
export function LockerCard({ className }: { className?: string }) {
  const { t } = useLocale()
  const copy = t.profile.locker
  const { session, setUser } = useAuth()
  const items = useAsync(api.myItems)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string>()
  if (session.status !== 'authenticated') return null
  const me = session.user

  async function wear(slot: EquipmentSlot, item: ShopItemDto | null) {
    setBusy(true)
    setError(undefined)
    try {
      setUser(await api.equip({ [slot]: item?.id ?? null }))
    } catch (caught) {
      setError(caught instanceof ApiError && caught.code === 'urn:app:shop/not-owned' ? t.shop.errors.notOwned : String((caught as Error).message))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Card className={className}>
      <CardHeader>
        <CardTitle>
          <h2>{copy.title}</h2>
        </CardTitle>
        <CardDescription>{copy.subtitle}</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-5">
        {error && (
          <p role="alert" className="text-sm text-destructive">
            {error}
          </p>
        )}
        {items.data ? (
          items.data.length ? (
            <Slots items={items.data} me={me} busy={busy} onWear={wear} />
          ) : (
            <div className="grid justify-items-start gap-3">
              <p className="text-sm text-muted-foreground">{copy.empty}</p>
              <Button asChild variant="outline">
                <Link to="/shop">{copy.toShop}</Link>
              </Button>
            </div>
          )
        ) : items.error ? (
          <p role="alert" className="text-sm text-destructive">
            {items.error.message}
          </p>
        ) : (
          <Skeleton aria-busy="true" className="h-48 rounded-lg" />
        )}
      </CardContent>
    </Card>
  )
}

interface SlotsProps {
  items: ShopItemDto[]
  me: MeDto
  busy: boolean
  onWear: (slot: EquipmentSlot, item: ShopItemDto | null) => void
}

// One slot at a time behind tabs, so a big collection stays one screen tall.
function Slots({ items, me, busy, onWear }: SlotsProps) {
  const { t } = useLocale()
  const sections = byKind(items)
  const [picked, setPicked] = useState<ShopItemKind>(sections[0].kind)
  const { kind, items: owned } = sections.find((section) => section.kind === picked) ?? sections[0]
  const slot = SLOTS[kind]!

  return (
    <div className="grid gap-4">
      <SegmentedControl
        label={t.profile.locker.slots}
        name="locker-slot"
        value={kind}
        onChange={setPicked}
        options={sections.map((section) => ({ value: section.kind, label: `${t.shop.kinds[section.kind]} · ${section.items.length}` }))}
      />
      <ul className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
        {owned.map((item) => {
          const worn = me.equipment[slot] === item.assetKey
          return (
            <li key={item.id} className={cn('grid gap-3 rounded-lg p-3 ring-1 ring-inset', worn ? 'bg-primary/5 ring-primary/60' : 'ring-border')}>
              <div className="grid h-24 place-items-center overflow-hidden rounded-md bg-secondary ring-1 ring-border ring-inset">
                <ItemPreview item={item} me={me} />
              </div>
              <div className="flex flex-wrap items-center gap-2">
                <span className="min-w-0 flex-1 text-sm font-medium">{item.name}</span>
                <RarityBadge rarity={item.rarity} />
              </div>
              <div className="flex flex-wrap items-center gap-2">
                <Button size="sm" variant={worn ? 'outline' : 'default'} aria-disabled={busy} onClick={() => !busy && onWear(slot, worn ? null : item)}>
                  {worn ? t.shop.unequip : t.shop.equip}
                </Button>
                {worn && (
                  <Badge variant="success">
                    <Check aria-hidden="true" />
                    {t.shop.equipped}
                  </Badge>
                )}
              </div>
            </li>
          )
        })}
      </ul>
    </div>
  )
}
