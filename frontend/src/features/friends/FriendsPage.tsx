import { CircleAlert, CircleCheck, Link2, LoaderCircle, Swords, UserPlus, Users } from 'lucide-react'
import { useEffect, useState, type SubmitEvent } from 'react'
import { Link } from 'react-router'
import { StatusIndicator } from '@/components/shared/StatusIndicator'
import { UserAvatar } from '@/components/shared/UserAvatar'
import { Button } from '@/components/ui/button'
import { Card, CardAction, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Dialog, DialogContent } from '@/components/ui/dialog'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { DialogHeading } from '@/features/play/dialog-parts'
import { usePoll } from '@/hooks/use-poll'
import { api, ApiError } from '@/lib/api-client'
import { nameClass } from '@/lib/cosmetics'
import { ratingText } from '@/lib/game'
import type { Messages } from '@/lib/i18n/en'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import type { FriendChallengeCreatedDto, FriendDto, FriendStatus } from '@/types/api'
import { ChallengeWaiting } from './ChallengeWaiting'

// Polled, so requests and who is online stay current (online = seen in the last 2 minutes).
const keepPolling = () => false
const SECTIONS: FriendStatus[] = ['pending_in', 'accepted', 'pending_out']

const errorText = (caught: unknown, t: Messages) => {
  if (!(caught instanceof ApiError)) return t.auth.errors.generic
  const errors = t.friends.errors
  switch (caught.code) {
    case 'urn:app:users/not-found':
      return errors.notFound
    case 'urn:app:friends/already':
      return errors.already
    case 'urn:app:friends/self':
      return errors.self
    case 'urn:app:friends/not-friends':
      return errors.notFriends
  }
  return caught.status === 0 ? t.common.serverUnreachable : caught.message
}

interface OpenChallenge extends FriendChallengeCreatedDto {
  friendName?: string
}

// Friends (game idea §13): requests both ways, who is online, and a friendly duel by code or straight to a friend.
// lg: the lists on the left, the add form in a narrow column on the right (it comes first in the DOM, so phones and
// screen readers keep the form on top).
export function FriendsPage() {
  const { t } = useLocale()
  const copy = t.friends
  const friends = usePoll(api.friends, 10_000, keepPolling)
  const [challenge, setChallenge] = useState<OpenChallenge>()
  const [error, setError] = useState<string>()

  async function createChallenge(friend?: FriendDto) {
    setError(undefined)
    try {
      const created = await api.createChallenge(friend ? { friendUserId: friend.user.id } : {})
      setChallenge({ ...created, friendName: friend?.user.username })
    } catch (caught) {
      setError(errorText(caught, t))
    }
  }

  return (
    <main className="mx-auto grid w-full max-w-5xl content-start gap-5 px-4 pb-10 sm:px-6 lg:px-8">
      <title>{`${copy.documentTitle} · StackOff`}</title>
      <header className="flex flex-wrap items-end gap-3">
        <div className="grid flex-1 gap-1">
          <h1 className="font-heading text-2xl font-semibold tracking-tight sm:text-3xl">{copy.title}</h1>
          <p className="text-sm text-pretty text-muted-foreground">{copy.subtitle}</p>
        </div>
        <Button variant="outline" onClick={() => createChallenge()}>
          <Link2 aria-hidden="true" />
          {copy.challengeLink}
        </Button>
      </header>
      {error && (
        <p
          role="alert"
          className="flex items-start gap-2 rounded-lg bg-destructive/10 px-3 py-2.5 text-sm text-destructive ring-1 ring-destructive/30 ring-inset"
        >
          <CircleAlert aria-hidden="true" className="mt-0.5 size-4 shrink-0" />
          {error}
        </p>
      )}

      <div className="grid items-start gap-5 lg:grid-cols-[minmax(0,1fr)_20rem]">
        <aside className="lg:col-start-2 lg:row-start-1">
          <AddFriend onAdded={friends.refresh} />
        </aside>
        <div className="grid content-start gap-5 lg:col-start-1 lg:row-start-1">
          {friends.data ? (
            friends.data.length ? (
              SECTIONS.map((status) => {
                const list = friends.data!.filter((friend) => friend.status === status)
                return list.length > 0 && <Section key={status} status={status} list={list} onChanged={friends.refresh} onChallenge={createChallenge} />
              })
            ) : (
              <Card size="sm" className="flex-row items-center gap-3 px-4">
                <span className="grid size-9 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary">
                  <Users aria-hidden="true" className="size-4" />
                </span>
                <div className="grid min-w-0 gap-0.5">
                  <h2 className="font-heading text-base font-semibold">{copy.emptyTitle}</h2>
                  <p className="text-sm text-pretty text-muted-foreground">{copy.empty}</p>
                </div>
              </Card>
            )
          ) : friends.error ? (
            <Card role="alert">
              <CardContent className="grid justify-items-start gap-3">
                <p>{copy.loadFailed}</p>
                <p className="text-sm text-muted-foreground">{friends.error.message}</p>
                <Button variant="outline" onClick={friends.refresh}>
                  {t.common.retry}
                </Button>
              </CardContent>
            </Card>
          ) : (
            // Shaped like the list it stands in for: three rows of avatar and two lines.
            <div aria-busy="true" className="grid gap-4 rounded-xl bg-card p-5 ring-1 ring-foreground/10">
              <p role="status" className="sr-only">
                {t.common.loading}
              </p>
              {Array.from({ length: 3 }, (_, index) => (
                <div key={index} className="flex items-center gap-3">
                  <Skeleton className="size-10 rounded-full" />
                  <div className="grid flex-1 gap-2">
                    <Skeleton className="h-3.5 w-32" />
                    <Skeleton className="h-3 w-48 max-w-full" />
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      <Dialog open={!!challenge} onOpenChange={(open) => !open && setChallenge(undefined)}>
        <DialogContent showCloseButton={false} className="gap-5 bg-card p-5 sm:max-w-lg sm:p-6">
          <DialogHeading title={copy.challenge.title} description={copy.challenge.subtitle} />
          {challenge && <ChallengeWaiting code={challenge.code} expiresAt={challenge.expiresAt} friendName={challenge.friendName} />}
        </DialogContent>
      </Dialog>
    </main>
  )
}

interface SectionProps {
  status: FriendStatus
  list: FriendDto[]
  onChanged: () => void
  onChallenge: (friend: FriendDto) => void
}

// Requests wait on me, so they get the sky ring and an icon; friends show how many are online; sent requests are quiet.
function Section({ status, list, onChanged, onChallenge }: SectionProps) {
  const { t } = useLocale()
  const copy = t.friends
  const online = list.filter((friend) => friend.online).length

  return (
    <Card size={status === 'pending_out' ? 'sm' : 'default'} className={cn(status === 'pending_in' && 'ring-primary/30')}>
      <CardHeader className={cn(status === 'pending_out' && 'px-4')}>
        <CardTitle className="flex items-center gap-3">
          {status === 'pending_in' && (
            <span className="grid size-9 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary">
              <UserPlus aria-hidden="true" className="size-4" />
            </span>
          )}
          <h2>{copy.sections[status](list.length)}</h2>
        </CardTitle>
        {status === 'accepted' && online > 0 && (
          <CardAction className="self-center">
            <StatusIndicator tone="ok" label={copy.onlineCount(online)} />
          </CardAction>
        )}
      </CardHeader>
      <CardContent className={cn(status === 'pending_out' && 'px-4')}>
        <ul className="divide-y divide-border rounded-lg ring-1 ring-border ring-inset">
          {list.map((friend) => (
            <FriendRow key={friend.user.id} friend={friend} onChanged={onChanged} onChallenge={() => onChallenge(friend)} />
          ))}
        </ul>
      </CardContent>
    </Card>
  )
}

function AddFriend({ onAdded }: { onAdded: () => void }) {
  const { t } = useLocale()
  const copy = t.friends.add
  const [sending, setSending] = useState(false)
  const [message, setMessage] = useState<{ text: string; ok: boolean }>()

  async function submit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    const form = event.currentTarget
    const username = String(new FormData(form).get('username') ?? '').trim()
    if (!username) return setMessage({ text: copy.empty, ok: false })
    setSending(true)
    setMessage(undefined)
    try {
      const friend = await api.addFriend({ username })
      setMessage({ text: friend.status === 'accepted' ? copy.accepted(friend.user.username) : copy.sent(friend.user.username), ok: true })
      form.reset()
      onAdded()
    } catch (caught) {
      setMessage({ text: errorText(caught, t), ok: false })
    } finally {
      setSending(false)
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <h2>{copy.title}</h2>
        </CardTitle>
      </CardHeader>
      <CardContent>
        <form className="grid gap-2" onSubmit={submit} noValidate>
          <Label htmlFor="friend-username">{copy.label}</Label>
          <div className="flex flex-wrap gap-2">
            <Input
              id="friend-username"
              name="username"
              autoComplete="off"
              autoCapitalize="none"
              spellCheck={false}
              maxLength={20}
              aria-invalid={message && !message.ok}
              aria-describedby="friend-message"
              onChange={() => message && setMessage(undefined)}
              className="min-w-0 flex-1 basis-48"
            />
            <Button type="submit" aria-disabled={sending} className="aria-disabled:opacity-50 max-sm:w-full lg:w-full">
              {sending ? <LoaderCircle aria-hidden="true" className="animate-spin motion-reduce:animate-none" /> : <UserPlus aria-hidden="true" />}
              {copy.send}
            </Button>
          </div>
          {/* The hint lives in the message line, so the line is never an empty gap: a result replaces it, typing brings it back. */}
          <p
            id="friend-message"
            role="status"
            className={cn('min-h-5 text-sm text-pretty', !message ? 'text-muted-foreground' : message.ok ? 'text-success' : 'text-destructive')}
          >
            <span key={message?.text ?? ''} className="flex items-start gap-1.5 animate-in fade-in duration-150 motion-reduce:animate-none">
              {message && (message.ok ? <CircleCheck aria-hidden="true" className="mt-0.5 size-4 shrink-0" /> : <CircleAlert aria-hidden="true" className="mt-0.5 size-4 shrink-0" />)}
              {message?.text ?? copy.subtitle}
            </span>
          </p>
        </form>
      </CardContent>
    </Card>
  )
}

function FriendRow({ friend, onChanged, onChallenge }: { friend: FriendDto; onChanged: () => void; onChallenge: () => void }) {
  const { t } = useLocale()
  const copy = t.friends
  const [busy, setBusy] = useState(false)
  const [confirming, setConfirming] = useState(false)
  const [error, setError] = useState<string>()
  const { user } = friend
  const sent = friend.status === 'pending_out'

  useEffect(() => {
    if (!confirming) return
    const id = window.setTimeout(() => setConfirming(false), 3000)
    return () => window.clearTimeout(id)
  }, [confirming])

  async function run(command: () => Promise<unknown>) {
    setBusy(true)
    setError(undefined)
    try {
      await command()
      onChanged()
    } catch (caught) {
      setError(errorText(caught, t))
    } finally {
      setBusy(false)
      setConfirming(false)
    }
  }
  const remove = () => run(() => api.removeFriend(user.id))

  return (
    <li className="flex flex-wrap items-center gap-3 px-3 py-2.5">
      <Link
        to={`/u/${user.username}`}
        className="flex min-w-0 flex-1 basis-48 items-center gap-3 rounded-md focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
      >
        <span className="relative shrink-0">
          <UserAvatar user={user} className={sent ? 'size-8' : 'size-10'} />
          {friend.status === 'accepted' && (
            <span
              aria-hidden="true"
              className={cn('absolute -right-0.5 -bottom-0.5 size-3 rounded-full ring-2 ring-card', friend.online ? 'bg-success' : 'bg-muted-foreground/60')}
            />
          )}
        </span>
        <span className="grid min-w-0">
          <span className={cn('truncate text-sm font-medium', nameClass(user))}>{user.username}</span>
          <span className="truncate text-xs text-muted-foreground">
            {user.title ?? t.dashboard.tiers[user.tier]} · <span className="font-mono tabular-nums">{ratingText(user.rating, user.isProvisional, t)}</span>
            {friend.status === 'accepted' && (
              <>
                {' · '}
                <span className={cn(friend.online && 'text-success')}>{friend.online ? copy.online : copy.offline}</span>
              </>
            )}
          </span>
        </span>
      </Link>
      {/* The quiet action first and the main one last, so a widening "Remove" grows to the left and Challenge stays put. */}
      <span className={cn('ml-auto flex flex-wrap items-center justify-end gap-2', !sent && 'max-sm:basis-full max-sm:*:flex-1')}>
        {friend.status === 'pending_in' && (
          <>
            <Button variant="ghost" aria-disabled={busy} onClick={() => !busy && remove()}>
              {copy.decline}
            </Button>
            <Button aria-disabled={busy} onClick={() => !busy && run(() => api.acceptFriend(user.id))}>
              {copy.accept}
            </Button>
          </>
        )}
        {friend.status === 'accepted' && (
          <>
            <Button
              variant={confirming ? 'destructive' : 'ghost'}
              aria-disabled={busy}
              onClick={() => (busy ? undefined : confirming ? remove() : setConfirming(true))}
            >
              {confirming ? copy.removeConfirm : copy.remove}
            </Button>
            {/* Filled only for friends who are online: a column of sky buttons would drown the one that matters. */}
            <Button variant={friend.online ? 'default' : 'outline'} onClick={onChallenge}>
              <Swords aria-hidden="true" />
              {copy.challengeFriend}
            </Button>
          </>
        )}
        {sent && (
          <>
            <span className="rounded-md bg-secondary px-1.5 py-0.5 font-mono text-xs text-muted-foreground uppercase">{copy.waiting}</span>
            <Button variant="ghost" aria-disabled={busy} onClick={() => !busy && remove()}>
              {copy.cancel}
            </Button>
          </>
        )}
      </span>
      {error && (
        <p role="alert" className="basis-full text-sm text-destructive">
          {error}
        </p>
      )}
    </li>
  )
}
