import { Check, Copy, LoaderCircle } from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'
import { Button } from '@/components/ui/button'
import { useOpenMatch } from '@/features/match/use-start-match'
import { usePoll } from '@/hooks/use-poll'
import { api } from '@/lib/api-client'
import { relativeTime } from '@/lib/format'
import { useLocale } from '@/lib/i18n/locale-context'
import type { FriendChallengeDto } from '@/types/api'

const joined = (challenge: FriendChallengeDto) => !!challenge.matchId

// The host's side of a friend challenge: the code and link to share, then the duel once someone joins (polled 2 s).
export function ChallengeWaiting({ code, expiresAt, friendName }: { code: string; expiresAt: string; friendName?: string }) {
  const { t, locale } = useLocale()
  const copy = t.friends.challenge
  const openMatch = useOpenMatch()
  const load = useCallback((signal: AbortSignal) => api.challenge(code, signal), [code])
  const { data } = usePoll(load, 2000, joined)
  const [copied, setCopied] = useState(false)
  const link = `${window.location.origin}/challenge/${code}`

  useEffect(() => {
    if (data?.matchId) openMatch(data.matchId)
  }, [data?.matchId, openMatch])

  useEffect(() => {
    if (!copied) return
    const id = window.setTimeout(() => setCopied(false), 2000)
    return () => window.clearTimeout(id)
  }, [copied])

  async function copyLink() {
    await navigator.clipboard.writeText(link).catch(() => {})
    setCopied(true)
  }

  return (
    <div className="grid justify-items-center gap-4 text-center">
      <p className="text-sm text-pretty text-muted-foreground">{friendName ? copy.sentTo(friendName) : copy.share}</p>
      <p className="font-mono text-4xl font-semibold tracking-[0.3em]" aria-label={copy.codeLabel(code.split('').join(' '))}>
        {code}
      </p>
      <div className="flex w-full max-w-md items-center gap-2 rounded-lg bg-secondary p-1.5 pl-3 ring-1 ring-border ring-inset">
        <span className="min-w-0 flex-1 truncate text-left font-mono text-xs">{link}</span>
        <Button size="sm" variant="outline" onClick={copyLink}>
          {copied ? <Check aria-hidden="true" /> : <Copy aria-hidden="true" />}
          {copied ? copy.copied : copy.copy}
        </Button>
      </div>
      <p role="status" className="flex items-center gap-2 text-sm text-muted-foreground">
        <LoaderCircle aria-hidden="true" className="size-4 animate-spin motion-reduce:animate-none" />
        {copy.waiting} · {copy.expires(relativeTime(expiresAt, locale))}
      </p>
    </div>
  )
}
