import { ArrowUp, BookOpen, GraduationCap, ListChecks, LoaderCircle, MessagesSquare, Plus, Square } from 'lucide-react'
import { useCallback, useEffect, useRef, useState, type KeyboardEvent, type ReactNode } from 'react'
import { Link, useSearchParams } from 'react-router'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { Textarea } from '@/components/ui/textarea'
import { useAsync, type AsyncState } from '@/hooks/use-async'
import { useTicker } from '@/hooks/use-ticker'
import { api } from '@/lib/api-client'
import { relativeTime } from '@/lib/format'
import { useLocale } from '@/lib/i18n/locale-context'
import { cn } from '@/lib/utils'
import type { ProfessorChatSummaryDto, ProfessorMessageDto } from '@/types/api'
import { Markdown } from './Markdown'
import { professorError } from './professor-error'
import { useCreateStudy, type Making, type StudyKind } from './use-create-study'

const MAX_LENGTH = 2000 // SendProfessorMessageRequest.text
const loadChats = (signal: AbortSignal) => api.professorChats(signal)
const NO_MESSAGES: ProfessorMessageDto[] = []

// The AI Professor (docs §6.12): a chat about IT next to the list of past chats. ?chat=<id> picks the chat, so the
// first answer of a new chat can name it without remounting the conversation that holds it.
export function ProfessorPage() {
  const { t } = useLocale()
  const [params, setParams] = useSearchParams()
  const chatId = params.get('chat') ?? undefined
  const chats = useAsync(loadChats)
  const { reload } = chats

  const onSaved = useCallback(
    (chat: ProfessorChatSummaryDto) => {
      setParams({ chat: chat.id }, { replace: true })
      reload()
    },
    [setParams, reload],
  )

  // The page never scrolls (data-fit-screen, see AppShell): the chat list and the messages scroll inside it, and the
  // question box stays at the bottom at any size or zoom.
  return (
    <main
      data-fit-screen
      className="mx-auto flex min-h-0 w-full max-w-7xl flex-1 flex-col px-4 sm:px-6 lg:grid lg:grid-cols-[16rem_minmax(0,1fr)] lg:grid-rows-[minmax(0,1fr)] lg:gap-6 lg:px-8"
    >
      <title>{`${t.professor.documentTitle} · StackOff`}</title>
      <aside className="hidden min-h-0 flex-col gap-4 pb-4 lg:flex">
        <Button asChild variant="outline" className="shrink-0 justify-start">
          <Link to="/professor">
            <Plus aria-hidden="true" />
            {t.professor.newChat}
          </Link>
        </Button>
        <ChatList chats={chats} current={chatId} />
      </aside>
      <div className="flex min-h-0 min-w-0 flex-1 flex-col gap-3 cramped:gap-2">
        <header className="flex shrink-0 flex-wrap items-center gap-3">
          <h1 className="font-heading text-2xl font-semibold tracking-tight sm:text-3xl cramped:sr-only">
            {t.professor.title}
          </h1>
          <div className="ml-auto flex items-center gap-2 lg:hidden [@media(max-height:15rem)]:hidden">
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="outline" className="cramped:h-8">
                  <MessagesSquare aria-hidden="true" />
                  {t.professor.chats}
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
            <Button asChild className="cramped:h-8">
              <Link to="/professor">
                <Plus aria-hidden="true" />
                {t.professor.newChat}
              </Link>
            </Button>
          </div>
        </header>
        <Conversation chatId={chatId} onSaved={onSaved} />
      </div>
    </main>
  )
}

function ChatList({ chats, current }: { chats: AsyncState<ProfessorChatSummaryDto[]>; current?: string }) {
  const { t, locale } = useLocale()

  return (
    <nav aria-label={t.professor.chats} className="-mx-1 grid min-h-0 flex-1 content-start gap-2 overflow-y-auto p-1">
      <h2 className="px-3 text-xs font-medium text-muted-foreground">{t.professor.chats}</h2>
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
      {/* relative: the screen-reader labels inside are absolutely positioned and would otherwise stretch the page. */}
      <div ref={scroller} onScroll={onScroll} className="relative min-h-0 flex-1 overflow-y-auto overscroll-contain pb-2">
        {status === 'loading' ? (
          <div aria-busy="true" className="grid gap-4">
            <p role="status" className="sr-only">
              {t.common.loading}
            </p>
            <Skeleton className="ml-auto h-12 w-2/3 rounded-2xl" />
            <Skeleton className="h-40 rounded-2xl" />
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
          <ol className="grid gap-6">
            {messages.map((message) => (
              <li key={message.id}>
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
                <li>
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
    <div className="ml-auto w-fit max-w-[85%] rounded-2xl rounded-br-sm bg-primary/15 px-4 py-3 break-words whitespace-pre-wrap">
      <span className="sr-only">{t.professor.you}: </span>
      {text}
    </div>
  )
}

function Answer({ busy, children }: { busy?: boolean; children: ReactNode }) {
  const { t } = useLocale()

  return (
    <div aria-busy={busy || undefined} className="flex gap-3">
      <ProfessorMark />
      <div className="grid min-w-0 flex-1 content-start gap-3">
        <p className="text-xs font-medium text-muted-foreground">{t.professor.professor}</p>
        {children}
      </div>
    </div>
  )
}

function ProfessorMark() {
  return (
    <span aria-hidden="true" className="grid size-9 shrink-0 place-items-center rounded-full bg-primary/15 text-primary">
      <GraduationCap className="size-5" />
    </span>
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

// "Create lesson" and "Create quiz" under an answer about IT; the one that fits the answer comes first (a lesson for
// a broad subject, a quiz for a concrete question). While one is written, its status takes their place.
function StudyActions({ message, study }: { message: ProfessorMessageDto; study: ReturnType<typeof useCreateStudy> }) {
  const { t } = useLocale()
  const topic = message.topic
  if (!topic) return null
  if (study.making?.messageId === message.id) return <MakingStatus making={study.making} />

  const lessonFirst = message.scope === 'general'
  const action = (kind: StudyKind, primary: boolean) => {
    const id = kind === 'lesson' ? message.lessonId : message.quizId
    const Icon = kind === 'lesson' ? BookOpen : ListChecks
    const variant = primary ? 'default' : 'outline'
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
      >
        <Icon aria-hidden="true" />
        {kind === 'lesson' ? t.professor.createLesson : t.professor.createQuiz}
      </Button>
    )
  }
  const error = study.error?.messageId === message.id ? study.error.text : undefined

  return (
    <div className="grid gap-2 pt-1">
      <div className="flex flex-wrap gap-2">
        {lessonFirst ? [action('lesson', true), action('quiz', false)] : [action('quiz', true), action('lesson', false)]}
      </div>
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
    </div>
  )
}

function MakingStatus({ making }: { making: Making }) {
  const { t } = useLocale()
  const now = useTicker(true, 1000)
  const seconds = Math.max(0, Math.floor((now - making.startedAt) / 1000))

  return (
    <div role="status" className="flex items-start gap-3 rounded-xl bg-primary/10 p-4 ring-1 ring-primary/30 ring-inset">
      <LoaderCircle aria-hidden="true" className="mt-0.5 size-5 shrink-0 animate-spin text-primary motion-reduce:animate-none" />
      <div className="grid gap-1">
        <p className="font-medium text-pretty">
          {making.kind === 'lesson' ? t.professor.makingLesson(making.topic) : t.professor.makingQuiz(making.topic)}
        </p>
        <p className="text-sm text-muted-foreground">
          {t.professor.makingHint}{' '}
          <span aria-hidden="true" className="font-mono tabular-nums">
            {t.professor.elapsed(seconds)}
          </span>
        </p>
      </div>
    </div>
  )
}

function Welcome({ onAsk }: { onAsk: (question: string) => void }) {
  const { t } = useLocale()

  return (
    <section className="grid justify-items-start gap-4 rounded-xl bg-card p-6 ring-1 ring-foreground/10">
      <ProfessorMark />
      <p className="max-w-prose text-lg text-pretty">{t.professor.subtitle}</p>
      <p className="text-sm text-muted-foreground">{t.professor.practiceOnly}</p>
      <h2 className="pt-2 text-xs font-medium text-muted-foreground">{t.professor.suggestionsLabel}</h2>
      <ul className="flex flex-wrap gap-2">
        {t.professor.suggestions.map((suggestion) => (
          <li key={suggestion}>
            <Button variant="outline" className="h-auto min-h-10 py-2 text-left whitespace-normal" onClick={() => onAsk(suggestion)}>
              {suggestion}
            </Button>
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
      className="grid shrink-0 gap-2 pb-4 cramped:pb-2"
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
          <Button key="send" type="submit" className="cramped:h-8" aria-disabled={disabled || !value.trim()}>
            <ArrowUp aria-hidden="true" />
            {t.professor.send}
          </Button>
        )}
      </div>
      <p id="professor-question-hint" className="text-xs text-muted-foreground cramped:sr-only">
        {t.professor.sendHint}
      </p>
    </form>
  )
}
