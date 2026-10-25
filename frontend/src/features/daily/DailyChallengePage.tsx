import { Check, Timer, X } from 'lucide-react'
import { useCallback, useEffect, useRef, useState } from 'react'
import { UserAvatar } from '@/components/shared/UserAvatar'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useAuth } from '@/features/auth/auth-context'
import { AnswerChip } from '@/features/match/AnswerChip'
import { Explanation } from '@/features/match/Explanation'
import { QuestionBody } from '@/features/match/QuestionBody'
import { useAsync } from '@/hooks/use-async'
import { useTicker } from '@/hooks/use-ticker'
import { api, ApiError } from '@/lib/api-client'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn, skyOutline } from '@/lib/utils'
import type { AnswerPayload, DailyChallengeDto } from '@/types/api'

const LIMIT_MS = 60_000
// The challenge day is local to Zagreb (GameRules.DayTimeZone), in the yyyy-mm-dd form the archive takes.
const today = () => new Intl.DateTimeFormat('sv-SE', { timeZone: 'Europe/Zagreb' }).format(new Date())
interface Running {
  dto: DailyChallengeDto
  at: number
}

// Reads today's state from the archive, which never starts the 60 s (404 until anyone has opened today's challenge).
// Opened earlier today and left before answering: the clock kept running, so it goes straight back to the question.
async function loadToday(signal: AbortSignal): Promise<{ archive: DailyChallengeDto | null; running?: Running }> {
  const archive = await api.dailyChallengeOn(today(), signal).catch((caught) => {
    if (caught instanceof ApiError && caught.status === 404) return null
    throw caught
  })
  if (archive && !archive.attempted && archive.startedAt)
    return { archive, running: { dto: await api.dailyChallenge(signal), at: performance.now() } }
  return { archive }
}

// One find-the-bug question a day, the same for everyone: 60 seconds, one attempt, then the key and today's ranking.
export function DailyChallengePage() {
  const { t } = useLocale()
  const challenge = useAsync(loadToday)
  const [started, setStarted] = useState<Running>()
  const [result, setResult] = useState<DailyChallengeDto>()
  const [error, setError] = useState<string>()

  const start = useCallback(async () => {
    setError(undefined)
    try {
      setStarted({ dto: await api.dailyChallenge(), at: performance.now() })
    } catch (caught) {
      setError(caught instanceof ApiError && caught.status === 0 ? t.common.serverUnreachable : String((caught as Error).message))
    }
  }, [t])

  const running = started ?? challenge.data?.running
  const done = result ?? (challenge.data?.archive?.attempted ? challenge.data.archive : undefined)

  return (
    <main className="mx-auto grid w-full max-w-3xl content-start gap-5 px-4 pb-10 sm:px-6 lg:px-8">
      <title>{`${t.daily.documentTitle} · StackOff`}</title>
      <header className="grid gap-1">
        <h1 className="font-heading text-2xl font-semibold tracking-tight sm:text-3xl">{t.daily.title}</h1>
        <p className="text-sm text-pretty text-muted-foreground">{t.daily.subtitle}</p>
      </header>
      {done ? (
        <Result challenge={done} />
      ) : running ? (
        <Attempt key={running.dto.day} {...running} onDone={setResult} onLate={() => challenge.reload()} />
      ) : challenge.error ? (
        <Card role="alert">
          <CardContent className="grid justify-items-start gap-3">
            <p>{t.daily.loadFailed}</p>
            <p className="text-sm text-muted-foreground">{challenge.error.message}</p>
            <Button variant="outline" onClick={challenge.reload}>
              {t.common.retry}
            </Button>
          </CardContent>
        </Card>
      ) : challenge.loading ? (
        <Skeleton aria-busy="true" className="h-64 rounded-xl" />
      ) : (
        <Card>
          <CardContent className="grid justify-items-center gap-4 py-6 text-center">
            <Timer aria-hidden="true" className="size-10 text-primary" />
            <p className="max-w-md text-pretty">{t.daily.intro}</p>
            {error && (
              <p role="alert" className="text-sm text-destructive">
                {error}
              </p>
            )}
            <Button size="lg" onClick={start}>
              {t.daily.start}
            </Button>
          </CardContent>
        </Card>
      )}
    </main>
  )
}

interface AttemptProps {
  dto: DailyChallengeDto
  at: number
  onDone: (result: DailyChallengeDto) => void
  onLate: () => void
}

// The 60 s run from startedAt on the server's clock. Running out sends an empty attempt, which records the timeout.
function Attempt({ dto, at, onDone, onLate }: AttemptProps) {
  const { t } = useLocale()
  const { refreshUser } = useAuth()
  const now = useTicker(true)
  const used = dto.startedAt ? Date.parse(dto.serverTime) - Date.parse(dto.startedAt) : 0
  const left = Math.max(0, LIMIT_MS - used - (now - at))
  const [sent, setSent] = useState<AnswerPayload>()
  const [error, setError] = useState<string>()
  const sending = useRef(false)

  const submit = useCallback(
    async (payload: AnswerPayload) => {
      if (sending.current) return
      sending.current = true
      setSent(payload)
      try {
        onDone(await api.dailyAttempt({ payload, clientElapsedMs: Math.round(used + performance.now() - at) }))
        refreshUser().catch(() => {})
      } catch (caught) {
        if (caught instanceof ApiError && (caught.code === 'urn:app:daily/too-late' || caught.code === 'urn:app:daily/already-attempted')) onLate()
        else {
          setError(caught instanceof ApiError && caught.status === 0 ? t.common.serverUnreachable : String((caught as Error).message))
          sending.current = false
          setSent(undefined)
        }
      }
    },
    [at, used, onDone, onLate, refreshUser, t],
  )

  useEffect(() => {
    const id = window.setTimeout(() => submit({ line: 0, fixId: '' }), Math.max(0, LIMIT_MS - used - (performance.now() - at)))
    return () => window.clearTimeout(id)
  }, [at, used, submit])

  const seconds = Math.ceil(left / 1000)
  return (
    <section className="grid gap-4">
      <div className="flex items-center gap-3">
        <div aria-hidden="true" className="h-2 flex-1 overflow-hidden rounded-full bg-secondary">
          <div
            className={cn('h-full rounded-full transition-[width] duration-100 ease-linear', seconds <= 10 ? 'bg-destructive' : 'bg-primary')}
            style={{ width: `${(left / LIMIT_MS) * 100}%` }}
          />
        </div>
        <span role="timer" className={cn('w-10 text-right font-mono text-sm tabular-nums', seconds <= 10 && 'text-destructive')}>
          {seconds} s
        </span>
      </div>
      <Card className={skyOutline}>
        <CardContent>
          <QuestionBody content={dto.question.content} language={dto.question.language} mine={sent} locked={!!sent || left <= 0} onAnswer={submit} />
        </CardContent>
      </Card>
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
    </section>
  )
}

function Result({ challenge }: { challenge: DailyChallengeDto }) {
  const { t } = useLocale()
  const copy = t.daily
  const mine = challenge.myAttempt

  return (
    <section className="grid gap-5">
      {mine && (
        <header aria-live="polite" className="flex flex-wrap items-center gap-3">
          <AnswerChip result={mine} />
          <span className="font-mono text-sm text-muted-foreground">{copy.time((mine.elapsedMs / 1000).toFixed(1))}</span>
          {mine.rank != null && <span className="font-heading text-lg font-semibold">{copy.rank(mine.rank)}</span>}
        </header>
      )}
      <Card className={skyOutline}>
        <CardContent className="grid gap-4">
          <QuestionBody content={challenge.question.content} language={challenge.question.language} mine={mine?.payload} correct={challenge.key?.answer} locked onAnswer={() => {}} />
          {challenge.key && (
            <p className="text-sm text-pretty">
              <span className="font-medium text-primary">{t.match.remember}:</span> {challenge.key.remember}
            </p>
          )}
          <Explanation questionId={challenge.question.id} initial={challenge.explanation} />
        </CardContent>
      </Card>
      <Card>
        <CardHeader>
          <CardTitle>
            <h2>{copy.leaderboard}</h2>
          </CardTitle>
          <CardDescription>{copy.leaderboardHint}</CardDescription>
        </CardHeader>
        <CardContent>
          {challenge.leaderboard.length ? (
            <ol className="grid gap-1.5">
              {challenge.leaderboard.map((entry) => (
                <li key={entry.user.id} className="flex items-center gap-3 rounded-lg px-3 py-2 ring-1 ring-border ring-inset">
                  <span className="w-6 font-mono text-sm text-muted-foreground tabular-nums">{entry.rank}</span>
                  <UserAvatar user={entry.user} className="size-8" />
                  <span className="min-w-0 flex-1 truncate text-sm font-medium">{entry.user.username}</span>
                  {entry.isCorrect ? (
                    <Check aria-label={copy.correct} className="size-4 text-success" />
                  ) : (
                    <X aria-label={copy.wrong} className="size-4 text-destructive" />
                  )}
                  <span className="w-14 text-right font-mono text-sm tabular-nums">{(entry.elapsedMs / 1000).toFixed(1)} s</span>
                </li>
              ))}
            </ol>
          ) : (
            <p className="text-sm text-muted-foreground">{copy.noOne}</p>
          )}
        </CardContent>
      </Card>
    </section>
  )
}
