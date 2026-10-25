import { ArrowUp, BookOpen, Crown, ListChecks, LoaderCircle, MessagesSquare, Plus, Square } from 'lucide-react'
import { useCallback, useEffect, useRef, useState, type KeyboardEvent, type ReactNode } from 'react'
import { Link, useSearchParams } from 'react-router'
import professor from '@/assets/professor.png'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { useAsync, type AsyncState } from '@/hooks/use-async'
import { useTicker } from '@/hooks/use-ticker'
import { api } from '@/lib/api-client'
import { idleHover, markStyles } from '@/features/match/question-utils'
import { clock, relativeTime } from '@/lib/format'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import type { ProfessorChatSummaryDto, ProfessorMessageDto, SubscriptionDto } from '@/types/api'
import { Markdown } from './Markdown'
import { professorError } from './professor-error'
import { useCreateStudy, type Making, type StudyKind } from './use-create-study'

const MAX_LENGTH = 2000 // SendProfessorMessageRequest.text
const loadChats = (signal: AbortSignal) => api.professorChats(signal)
const NO_MESSAGES: ProfessorMessageDto[] = []
// One reading column (about 80 characters a line) for the header, the messages and the question box.
const COLUMN = 'mx-auto w-full max-w-3xl'
// Button styles only `disabled`; these buttons stay focusable while blocked, so they say so themselves.
const blocked = 'aria-disabled:cursor-not-allowed aria-disabled:opacity-50'
const label = 'font-mono text-xs font-medium tracking-wide text-muted-foreground uppercase'

// The AI Professor (docs §6.12): a chat about IT next to the list of past chats. ?chat=<id> picks the chat, so the
// first answer of a new chat can name it without remounting the conversation that holds it.
export function ProfessorPage() {
  const { t } = useLocale()
  const [params, setParams] = useSearchParams()
  const chatId = params.get('chat') ?? undefined
  const chats = useAsync(loadChats)
  const usage = useAsync(api.subscription)
  const { reload } = chats
  const title = chats.data?.find((chat) => chat.id === chatId)?.title
  const { reload: reloadUsage } = usage

  const onSaved = useCallback(
    (chat: ProfessorChatSummaryDto) => {
      setParams({ chat: chat.id }, { replace: true })
      reload()
      reloadUsage() // a Junior developer's allowance moved
    },
    [setParams, reload, reloadUsage],
  )

  // The page never scrolls (data-fit-screen, see AppShell): the chat list and the messages scroll inside it, and the
  // question box stays at the bottom at any size or zoom.
  return (
    <main
      data-fit-screen
      className="mx-auto flex min-h-0 w-full max-w-7xl flex-1 flex-col px-4 sm:px-6 lg:grid lg:grid-cols-[16rem_minmax(0,1fr)] lg:grid-rows-[minmax(0,1fr)] lg:gap-6 lg:px-8"
    >
      <title>{`${t.professor.documentTitle} · StackOff`}</title>
      {/* A panel: "New chat" is its first row, lit while you are in a new chat, so exactly one row is always current. */}
      <aside className="mb-4 hidden min-h-0 flex-col gap-1 rounded-xl bg-card p-2 ring-1 ring-foreground/10 lg:flex">
        <Link
          to="/professor"
          aria-current={chatId ? undefined : 'page'}
          className={cn(
            'flex h-11 shrink-0 items-center gap-3 rounded-lg px-3 text-sm font-medium transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring',
            chatId ? 'hover:bg-secondary' : 'bg-primary/15 text-primary',
          )}
        >
          <span aria-hidden="true" className="grid size-7 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary">
            <Plus className="size-4" />
          </span>
          {t.professor.newChat}
        </Link>
        <ChatList chats={chats} current={chatId} />
      </aside>
      <div className="flex min-h-0 min-w-0 flex-1 flex-col gap-3 cramped:gap-2">
        <header className={cn(COLUMN, 'flex shrink-0 flex-wrap items-center gap-3')}>
          <h1 className="font-heading text-2xl font-semibold tracking-tight cramped:sr-only">{t.professor.title}</h1>
          <div className="ml-auto flex min-w-0 items-center gap-2 lg:hidden [@media(max-height:15rem)]:hidden">
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="outline" className="min-w-0 cramped:h-8 max-sm:h-11">
                  <MessagesSquare aria-hidden="true" />
                  <span className="sr-only">{t.professor.chats}: </span>
                  <span className="max-w-32 truncate sm:max-w-48">{title ?? t.professor.chats}</span>
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-72 max-w-[calc(100vw-2rem)]">
                {chats.data?.length ? (
                  chats.data.map((chat) => (
                    <DropdownMenuItem key={chat.id} asChild className={cn('py-3', chat.id === chatId && 'text-primary')}>
                      <Link to={`/professor?chat=${chat.id}`}>
                        <span className="truncate">{chat.title}</span>
                      </Link>
                    </DropdownMenuItem>
                  ))
                ) : (
                  <p className="px-2 py-3 text-sm text-muted-foreground">{chats.error ? t.professor.chatsFailed : t.professor.noChats}</p>
                )}
              </DropdownMenuContent>
            </DropdownMenu>
            <Button asChild className="cramped:h-8 max-sm:size-11 max-sm:px-0">
              <Link to="/professor">
                <Plus aria-hidden="true" />
                <span className="max-sm:sr-only">{t.professor.newChat}</span>
              </Link>
            </Button>
          </div>
        </header>
        <Usage usage={usage} />
        <Conversation chatId={chatId} onSaved={onSaved} />
      </div>
    </main>
  )
}

// A Junior developer's allowance for today (2 questions, 1 lesson, 1 quiz) with the way to lift it; nothing for a Senior.
function Usage({ usage }: { usage: AsyncState<SubscriptionDto> }) {
  const { t } = useLocale()
  const data = usage.data
  if (!data || data.plan === 'senior') return null
  const p = data.professor

  return (
    <p className={cn(COLUMN, 'flex shrink-0 flex-wrap items-center gap-x-3 gap-y-1 text-xs text-muted-foreground cramped:hidden')}>
      <span>{t.professor.usage(p.questionsUsed, p.questionsLimit ?? 0, p.lessonsUsed, p.lessonsLimit ?? 0, p.quizzesUsed, p.quizzesLimit ?? 0)}</span>
      <Link
        to="/senior"
        className="inline-flex items-center gap-1 rounded-sm font-medium text-warning hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
      >
        <Crown aria-hidden="true" className="size-3.5" />
        {t.professor.goSenior}
      </Link>
    </p>
  )
}

function ChatList({ chats, current }: { chats: AsyncState<ProfessorChatSummaryDto[]>; current?: string }) {
  const { t, locale } = useLocale()

  return (
    <nav aria-label={t.professor.chats} className="-mx-1 mt-1 grid min-h-0 flex-1 content-start gap-2 overflow-y-auto border-t border-border p-1 pt-3">
      <h2 className={cn(label, 'px-3')}>{t.professor.chats}</h2>
      {chats.data ? (
        chats.data.length === 0 ? (
          <p className="px-3 text-sm text-muted-foreground">{t.professor.noChats}</p>
        ) : (
          <ul className="grid gap-1">
            {chats.data.map((chat) => (
              <li key={chat.id}>
                <Link
                  to={`/professor?chat=${chat.id}`}
                  aria-current={chat.id === current ? 'page' : undefined}
                  className={cn(
                    'grid gap-0.5 rounded-lg px-3 py-2 text-sm transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring',
                    chat.id === current ? 'bg-primary/15 text-primary' : 'hover:bg-secondary',
                  )}
                >
                  <span className="truncate font-medium">{chat.title}</span>
                  <span className="text-xs text-muted-foreground">{relativeTime(chat.updatedAt, locale)}</span>
                </Link>
              </li>
            ))}
          </ul>
        )
      ) : chats.error ? (
        <p className="px-3 text-sm text-destructive">{t.professor.chatsFailed}</p>
      ) : (
        <div aria-busy="true" className="grid gap-2">
          <Skeleton className="h-12 rounded-lg" />
          <Skeleton className="h-12 rounded-lg" />
        </div>
      )}
    </nav>
  )
}

interface Pending {
  chatId?: string // the chat it was asked in; none for a new chat
  question: string
  answer: string // what has streamed so far
  error?: string
}

// The chat on screen. `view` holds the last chat loaded or created here; the URL catching up with a chat this view
// just created already matches it, so nothing reloads mid-conversation. What shows (loading, failed, the messages)
// follows from the URL and `view`. A turn is shown live while it streams and replaced by the saved messages. Leaving
// a chat stops its answer (nothing of it is saved).
function Conversation({ chatId, onSaved }: { chatId?: string; onSaved: (chat: ProfessorChatSummaryDto) => void }) {
  const { t } = useLocale()
  const [view, setView] = useState<{ chatId?: string; messages: ProfessorMessageDto[] }>({ messages: [] })
  const [failedFor, setFailedFor] = useState<string>()
  const [loads, setLoads] = useState(0)
  const [pending, setPending] = useState<Pending>()
  const [draft, setDraft] = useState('')
  const shown = useRef<string | undefined>(undefined) // view.chatId, for the effect
  const stream = useRef<AbortController | undefined>(undefined)
  const scroller = useRef<HTMLDivElement>(null)
  const stick = useRef(true)
  const study = useCreateStudy()

  // A chat created here shows at once: the router applies the new URL in a transition, a render later.
  const [urlChat, setUrlChat] = useState(chatId)
  const [created, setCreated] = useState<string>()
  if (urlChat !== chatId) {
    setUrlChat(chatId)
    setCreated(undefined)
  }
  const current = chatId ?? created
  const status = !current || view.chatId === current ? 'ready' : failedFor === current ? 'failed' : 'loading'
  const messages = current && view.chatId === current ? view.messages : NO_MESSAGES
  const turn = pending?.chatId === current ? pending : undefined // a turn of another chat stays off screen

  useEffect(() => {
    if (chatId === shown.current) return
    stream.current?.abort() // the answer belongs to the chat being left
    shown.current = undefined
    if (!chatId) return
    const controller = new AbortController()
    api.professorChat(chatId, controller.signal).then(
      (chat) => {
        shown.current = chat.id
        stick.current = true
        setView({ chatId: chat.id, messages: chat.messages })
      },
      () => {
        if (!controller.signal.aborted) setFailedFor(chatId)
      },
    )
    return () => controller.abort()
  }, [chatId, loads])

  useEffect(() => () => stream.current?.abort(), [])

  // Follow the answer as it grows, unless the player has scrolled up to read.
  function onScroll() {
    const box = scroller.current
    if (box) stick.current = box.scrollTop + box.clientHeight >= box.scrollHeight - 160
  }
  useEffect(() => {
    const box = scroller.current
    if (box && stick.current) box.scrollTop = box.scrollHeight
  }, [messages, turn])

  const streaming = turn !== undefined && turn.error === undefined

  async function send(text: string) {
    const question = text.trim()
    if (!question || streaming || status !== 'ready') return
    const controller = new AbortController()
    stream.current = controller
    stick.current = true
    setPending({ chatId: current, question, answer: '' })
    setDraft('')
    try {
      const saved = await api.askProfessor(
        { chatId: current ?? null, text: question },
        (delta) => setPending((previous) => previous && { ...previous, answer: previous.answer + delta }),
        controller.signal,
      )
      shown.current = saved.chat.id
      setCreated(saved.chat.id)
      setView((previous) => ({
        chatId: saved.chat.id,
        messages: [...(previous.chatId === saved.chat.id ? previous.messages : []), saved.question, saved.answer],
      }))
      setPending(undefined)
      onSaved(saved.chat)
    } catch (caught) {
      // Stopped, or the player opened another chat: nothing was saved, so nothing is left to show.
      if (controller.signal.aborted) setPending((previous) => (previous?.question === question ? undefined : previous))
      else setPending((previous) => previous && { ...previous, error: professorError(caught, t) })
    }
  }

  // Nothing of a stopped answer is saved, so the question goes back into the box unless something new is typed there.
  function stop() {
    stream.current?.abort()
    if (turn) setDraft((typed) => (typed.trim() ? typed : turn.question))
    setPending(undefined)
  }

  return (
    <div className="flex min-h-0 flex-1 flex-col gap-3 cramped:gap-2">
      {/* relative: the screen-reader labels inside are absolutely positioned and would otherwise stretch the page.
          The gutter on both edges keeps the column centred under the header whether or not a scrollbar shows, and
          px-1 leaves room for focus outlines that overflow would clip. */}
      <div
        ref={scroller}
        onScroll={onScroll}
        className="relative flex min-h-0 flex-1 flex-col overflow-y-auto px-1 [scrollbar-gutter:stable_both-edges]"
      >
        <div className={cn(COLUMN, 'flex flex-1 flex-col pb-2')}>
          {status === 'loading' ? (
            <div aria-busy="true" className="grid gap-4">
              <p role="status" className="sr-only">
                {t.common.loading}
              </p>
              <Skeleton className="ml-auto h-12 w-2/3 rounded-xl" />
              <Skeleton className="h-40 rounded-xl" />
            </div>
          ) : status === 'failed' ? (
            <Card role="alert">
              <CardContent className="grid justify-items-start gap-3">
                <p>{t.professor.chatFailed}</p>
                <Button
                  variant="outline"
                  onClick={() => {
                    setFailedFor(undefined)
                    setLoads((n) => n + 1)
                  }}
                >
                  {t.common.retry}
                </Button>
              </CardContent>
            </Card>
          ) : messages.length === 0 && !turn ? (
            <Welcome onAsk={send} />
          ) : (
            // A question sits close to its answer; the next question starts a new block.
            <ol className="grid gap-3">
              {messages.map((message) => (
                <li key={message.id} className={cn(message.role === 'user' && 'not-first:pt-6')}>
                  {message.role === 'user' ? (
                    <Question text={message.text} />
                  ) : (
                    <Answer>
                      <Markdown text={message.text} />
                      <StudyActions message={message} study={study} />
                    </Answer>
                  )}
                </li>
              ))}
              {turn && (
                <>
                  <li className="not-first:pt-6">
                    <Question text={turn.question} />
                  </li>
                  <li>
                    <Answer busy={streaming}>
                      {turn.answer && <Markdown text={turn.answer} />}
                      {turn.error ? (
                        <div role="alert" className="grid justify-items-start gap-3">
                          <p className="text-sm text-destructive">{turn.error}</p>
                          <Button variant="outline" onClick={() => send(turn.question)}>
                            {t.professor.retry}
                          </Button>
                        </div>
                      ) : turn.answer ? (
                        <p role="status" className="sr-only">
                          {t.professor.writing}
                        </p>
                      ) : (
                        <Thinking />
                      )}
                    </Answer>
                  </li>
                </>
              )}
            </ol>
          )}
        </div>
      </div>
      <Composer
        value={draft}
        onChange={setDraft}
        onSend={() => send(draft)}
        onStop={stop}
        streaming={streaming}
        disabled={status !== 'ready'}
      />
    </div>
  )
}

function Question({ text }: { text: string }) {
  const { t } = useLocale()

  return (
    // Neutral, so sky stays for what can be done next.
    <div className="ml-auto w-fit max-w-[min(85%,36rem)] rounded-xl rounded-br-md bg-secondary px-4 py-2.5 text-pretty break-words whitespace-pre-wrap ring-1 ring-border ring-inset">
      <span className="sr-only">{t.professor.you}: </span>
      {text}
    </div>
  )
}

// The speaker on its own row, then the answer as a card: full width on a phone, and code blocks keep their contrast.
function Answer({ busy, children }: { busy?: boolean; children: ReactNode }) {
  const { t } = useLocale()

  return (
    <div aria-busy={busy || undefined} className="grid gap-2">
      <p className={cn(label, 'flex items-center gap-2')}>
        <ProfessorMark className="size-9" />
        {t.professor.professor}
      </p>
      <div className="grid min-w-0 content-start gap-4 rounded-xl rounded-tl-md bg-card p-4 ring-1 ring-foreground/10 ring-inset sm:p-5">
        {children}
      </div>
    </div>
  )
}

// The professor's portrait, on the sky tint the cap icon had.
function ProfessorMark({ className }: { className: string }) {
  return (
    <img
      src={professor}
      alt=""
      draggable={false}
      className={cn('shrink-0 rounded-full bg-primary/15 object-cover ring-1 ring-primary/30', className)}
    />
  )
}

function Thinking() {
  const { t } = useLocale()

  return (
    <p role="status" className="flex items-center gap-2 text-sm text-muted-foreground">
      <span aria-hidden="true" className="flex gap-1">
        {[0, 150, 300].map((delay) => (
          <span
            key={delay}
            style={{ animationDelay: `${delay}ms` }}
            className="size-1.5 animate-pulse rounded-full bg-primary motion-reduce:animate-none"
          />
        ))}
      </span>
      {t.professor.thinking}
    </p>
  )
}

// The answer card's footer, edge to edge like a card footer.
const studyBar = '-mx-4 -mb-4 grid gap-2 border-t border-border px-4 pt-3 pb-4 sm:-mx-5 sm:-mb-5 sm:px-5 sm:pb-5'

// "Create lesson" and "Create quiz" under an answer about IT, next to its topic; the one that fits the answer comes
// first (a lesson for a broad subject, a quiz for a concrete question). While one is written, its status takes their
// place in the same bar, so nothing jumps.
function StudyActions({ message, study }: { message: ProfessorMessageDto; study: ReturnType<typeof useCreateStudy> }) {
  const { t } = useLocale()
  const topic = message.topic
  if (!topic) return null
  if (study.making?.messageId === message.id)
    return (
      <div className={studyBar}>
        <MakingStatus making={study.making} />
      </div>
    )

  const lessonFirst = message.scope === 'general'
  const action = (kind: StudyKind, primary: boolean) => {
    const id = kind === 'lesson' ? message.lessonId : message.quizId
    const Icon = kind === 'lesson' ? BookOpen : ListChecks
    const variant = primary ? 'default' : 'secondary'
    return id ? (
      <Button key={kind} asChild variant={variant}>
        <Link to={`/professor/${kind === 'lesson' ? 'lessons' : 'quizzes'}/${id}`}>
          <Icon aria-hidden="true" />
          {kind === 'lesson' ? t.professor.openLesson : t.professor.openQuiz}
        </Link>
      </Button>
    ) : (
      <Button
        key={kind}
        variant={variant}
        aria-disabled={study.making !== undefined}
        onClick={() => study.make(kind, message.id, topic)}
        className={blocked}
      >
        <Icon aria-hidden="true" />
        {kind === 'lesson' ? t.professor.createLesson : t.professor.createQuiz}
      </Button>
    )
  }
  const error = study.error?.messageId === message.id ? study.error.text : undefined

  return (
    <div className={studyBar}>
      <div className="flex flex-wrap items-center gap-2">
        <span className="max-w-full truncate rounded-md bg-secondary px-2 py-1 font-mono text-xs text-muted-foreground">{topic}</span>
        <div className="grid w-full grid-cols-2 gap-2 sm:ml-auto sm:flex sm:w-auto">
          {lessonFirst ? [action('lesson', true), action('quiz', false)] : [action('quiz', true), action('lesson', false)]}
        </div>
      </div>
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
    </div>
  )
}

// The time runs in its own slot on the right (0:12), the same clock as the ranked queue.
function MakingStatus({ making }: { making: Making }) {
  const { t } = useLocale()
  const now = useTicker(true, 1000)

  return (
    <div role="status" className="grid grid-cols-[auto_minmax(0,1fr)_auto] items-center gap-x-3 gap-y-0.5">
      <LoaderCircle aria-hidden="true" className="size-4 animate-spin text-primary motion-reduce:animate-none" />
      <p className="text-sm font-medium text-pretty">
        {making.kind === 'lesson' ? t.professor.makingLesson(making.topic) : t.professor.makingQuiz(making.topic)}
      </p>
      <span aria-hidden="true" className="font-mono text-xs text-muted-foreground tabular-nums">
        {clock(Math.max(0, now - making.startedAt))}
      </span>
      <p className="col-start-2 text-xs text-muted-foreground">{t.professor.makingHint}</p>
    </div>
  )
}

// Centred in the empty conversation, right above the question box; the suggestions are tiles like the answers in a
// duel, and the arrow says a tap sends them.
function Welcome({ onAsk }: { onAsk: (question: string) => void }) {
  const { t } = useLocale()

  return (
    <section className="my-auto grid justify-items-center gap-4 py-8 text-center">
      <ProfessorMark className="size-16" />
      <p className="max-w-xl font-heading text-xl font-semibold text-balance sm:text-2xl">{t.professor.subtitle}</p>
      <p className="rounded-md bg-secondary px-2 py-1 font-mono text-xs text-muted-foreground">{t.professor.practiceOnly}</p>
      <h2 className={cn(label, 'pt-4')}>{t.professor.suggestionsLabel}</h2>
      <ul className="grid w-full auto-rows-fr gap-2 sm:grid-cols-2">
        {t.professor.suggestions.map((suggestion) => (
          <li key={suggestion}>
            <button
              type="button"
              onClick={() => onAsk(suggestion)}
              className={cn(
                'group flex h-full min-h-14 w-full items-center gap-3 rounded-lg px-4 py-3 text-left text-sm font-medium ring-inset transition-[background-color,box-shadow,scale] duration-150 ease-out focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring enabled:active:scale-[0.98] motion-reduce:transition-none',
                markStyles.idle,
                idleHover,
              )}
            >
              <span className="min-w-0 flex-1 text-pretty">{suggestion}</span>
              <ArrowUp aria-hidden="true" className="size-4 shrink-0 text-muted-foreground transition-colors group-hover:text-primary" />
            </button>
          </li>
        ))}
      </ul>
    </section>
  )
}

interface ComposerProps {
  value: string
  onChange: (value: string) => void
  onSend: () => void
  onStop: () => void
  streaming: boolean
  disabled: boolean
}

// Enter sends and Shift+Enter starts a new line; while an answer streams, the button stops it.
function Composer({ value, onChange, onSend, onStop, streaming, disabled }: ComposerProps) {
  const { t } = useLocale()

  function onKeyDown(event: KeyboardEvent<HTMLTextAreaElement>) {
    if (event.key !== 'Enter' || event.shiftKey || event.nativeEvent.isComposing) return
    event.preventDefault()
    onSend()
  }

  return (
    <form
      onSubmit={(event) => {
        event.preventDefault()
        onSend()
      }}
      className={cn(COLUMN, 'grid shrink-0 gap-2 pb-4 cramped:pb-2')}
    >
      <Label htmlFor="professor-question" className="sr-only">
        {t.professor.inputLabel}
      </Label>
      <div className="flex items-end gap-2 rounded-xl bg-card p-2 ring-1 cramped:p-1 ring-foreground/10 focus-within:ring-2 focus-within:ring-ring">
        <Textarea
          id="professor-question"
          value={value}
          onChange={(event) => onChange(event.target.value)}
          onKeyDown={onKeyDown}
          rows={1}
          maxLength={MAX_LENGTH}
          placeholder={t.professor.placeholder}
          aria-describedby="professor-question-hint"
          className="max-h-[min(12rem,30dvh)] min-h-10 resize-none cramped:min-h-8 border-0 bg-transparent shadow-none focus-visible:border-0 focus-visible:ring-0 dark:bg-transparent"
        />
        {/* Separate keys: reusing one <button> would turn it into "submit" before the Stop click's default action
            runs, and the form would send the box again. */}
        {streaming ? (
          <Button key="stop" type="button" variant="outline" className="cramped:h-8" onClick={onStop}>
            <Square aria-hidden="true" />
            {t.professor.stop}
          </Button>
        ) : (
          <Button key="send" type="submit" className={cn('cramped:h-8', blocked)} aria-disabled={disabled || !value.trim()}>
            <ArrowUp aria-hidden="true" />
            {t.professor.send}
          </Button>
        )}
      </div>
      {/* Touch keyboards have no Shift+Enter; aria-describedby still reads the hint when it is hidden. */}
      <p id="professor-question-hint" className="text-xs text-muted-foreground cramped:sr-only pointer-coarse:hidden">
        {t.professor.sendHint}
      </p>
    </form>
  )
}
