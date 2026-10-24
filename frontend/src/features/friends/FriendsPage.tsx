import { Link2, Swords, UserPlus } from 'lucide-react'
import { useEffect, useState, type SubmitEvent } from 'react'
import { Link } from 'react-router'
import { UserAvatar } from '@/components/shared/UserAvatar'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
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
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}

      <AddFriend onAdded={friends.refresh} />

      {friends.data ? (
        friends.data.length ? (
          SECTIONS.map((status) => {
            const list = friends.data!.filter((friend) => friend.status === status)
            return (
              list.length > 0 && (
                <Card key={status}>
                  <CardHeader>
                    <CardTitle>
                      <h2>{copy.sections[status](list.length)}</h2>
                    </CardTitle>
                  </CardHeader>
                  <CardContent>
                    <ul className="grid gap-2">
                      {list.map((friend) => (
                        <FriendRow key={friend.user.id} friend={friend} onChanged={friends.refresh} onChallenge={() => createChallenge(friend)} />
                      ))}
                    </ul>
                  </CardContent>
                </Card>
              )
            )
          })
        ) : (
          <Card>
            <CardContent>
              <p className="text-sm text-muted-foreground">{copy.empty}</p>
            </CardContent>
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
        <Skeleton aria-busy="true" className="h-64 rounded-xl" />
      )}

      <Dialog open={!!challenge} onOpenChange={(open) => !open && setChallenge(undefined)}>
        <DialogContent showCloseButton={false} className="gap-5 bg-card p-5 sm:max-w-lg sm:p-6">
          <DialogHeading title={copy.challenge.title} description={copy.challenge.subtitle} />
          {challenge && <ChallengeWaiting code={challenge.code} expiresAt={challenge.expiresAt} friendName={challenge.friendName} />}
        </DialogContent>
      </Dialog>
    </main>
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
        <CardDescription>{copy.subtitle}</CardDescription>
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
              className="min-w-0 flex-1 basis-48"
            />
            <Button type="submit" aria-disabled={sending}>
              <UserPlus aria-hidden="true" />
              {copy.send}
            </Button>
          </div>
          <p id="friend-message" role="status" className={cn('min-h-5 text-sm', message?.ok ? 'text-success' : 'text-destructive')}>
            {message?.text}
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
    <li className="flex flex-wrap items-center gap-3 rounded-lg px-3 py-2.5 ring-1 ring-border ring-inset">
      <Link
        to={`/u/${user.username}`}
        className="flex min-w-0 flex-1 basis-48 items-center gap-3 rounded-md focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
      >
        <span className="relative shrink-0">
          <UserAvatar user={user} className="size-10" />
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
            {user.title ?? t.dashboard.tiers[user.tier]} · {ratingText(user.rating, user.isProvisional)}
            {friend.status === 'accepted' && ` · ${friend.online ? copy.online : copy.offline}`}
          </span>
        </span>
      </Link>
      <span className="flex flex-wrap items-center gap-2">
        {friend.status === 'pending_in' && (
          <>
            <Button size="sm" aria-disabled={busy} onClick={() => !busy && run(() => api.acceptFriend(user.id))}>
              {copy.accept}
            </Button>
            <Button size="sm" variant="ghost" aria-disabled={busy} onClick={() => !busy && remove()}>
              {copy.decline}
            </Button>
          </>
        )}
        {friend.status === 'accepted' && (
          <>
            <Button size="sm" onClick={onChallenge}>
              <Swords aria-hidden="true" />
              {copy.challengeFriend}
            </Button>
            <Button
              size="sm"
              variant={confirming ? 'destructive' : 'ghost'}
              aria-disabled={busy}
              onClick={() => (busy ? undefined : confirming ? remove() : setConfirming(true))}
            >
              {confirming ? copy.removeConfirm : copy.remove}
            </Button>
          </>
        )}
        {friend.status === 'pending_out' && (
          <>
            <Badge variant="outline">{copy.waiting}</Badge>
            <Button size="sm" variant="ghost" aria-disabled={busy} onClick={() => !busy && remove()}>
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
