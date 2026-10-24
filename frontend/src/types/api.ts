// Mirrors the backend contracts (App.Api / App.Application) and docs/tehnicki-dokument.md §6. Keep in sync when endpoints change.

export type HealthStatus = 'Healthy' | 'Degraded' | 'Unhealthy'

export interface HealthResponse {
  status: HealthStatus
  checks: Record<string, HealthStatus>
}

// RFC 9457 Problem Details, returned by the API for every error response.
// `type` is "urn:app:<code>" for game-rule and auth errors (see AppErrorCode).
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  errors?: Record<string, string[]>
}

export type AppErrorCode =
  | 'urn:app:topics/count'
  | 'urn:app:topics/locked-in-queue'
  | 'urn:app:topics/not-found'
  | 'urn:app:auth/invalid-credentials'
  | 'urn:app:auth/email-taken'
  | 'urn:app:auth/username-taken'
  | 'urn:app:auth/refresh-invalid'
  | 'urn:app:auth/reset-token-invalid'
  | 'urn:app:topics/not-playable'
  | 'urn:app:match/not-found'
  | 'urn:app:match/not-participant'
  | 'urn:app:match/already-in-match'
  | 'urn:app:match/wrong-phase' // late or duplicate request: refetch the state, do not show an error
  | 'urn:app:match/wrong-index'
  | 'urn:app:match/already-answered'
  | 'urn:app:match/not-finished'
  | 'urn:app:match/nothing-to-calibrate'
  | 'urn:app:question/not-found'
  | 'urn:app:question/not-answered'
  | 'urn:app:lesson/not-found'
  | 'urn:app:lesson/nothing-missed'
  | 'urn:app:lesson/already-passed'
  | 'urn:app:lesson/retry-too-soon'
  | 'urn:app:match/not-your-turn'
  | 'urn:app:match/topic-not-in-pool'
  | 'urn:app:queue/already-in'
  | 'urn:app:proposal/expired' // the offer was declined by the other player or ran out: poll the queue again

export type Uuid = string
export type Iso = string
export type Tier = 'bronze' | 'silver' | 'gold' | 'platinum' | 'diamond' | 'master' | 'grandmaster'
export type ProfileVisibility = 'public' | 'friends'
export type EquipmentSlot = 'avatarFrame' | 'banner' | 'boardTheme' | 'nameColor' | 'cardBackground'

export interface TopicDto {
  id: Uuid
  slug: string
  name: string
  description: string
  conceptCount: number
}

export interface ConceptDto {
  id: Uuid
  slug: string
  name: string
  topicId: Uuid
}

export interface UserRefDto {
  id: Uuid
  username: string
  avatar: string
  tier: Tier
  rating: number
  isProvisional: boolean
  title?: string | null
  equipment: Partial<Record<EquipmentSlot, string>> // assetKey per slot
}

export interface StreakDto {
  count: number
  lastActiveOn?: string | null
  freezes: number
  securedToday: boolean
}

export interface MeDto extends UserRefDto {
  email: string
  locale: 'en' | 'hr'
  profileVisibility: ProfileVisibility
  notifications: Record<string, boolean>
  rankedMatches: number
  coins: number
  xp: number
  level: number
  xpToNextLevel: number
  seasonPoints: number
  streak: StreakDto
  activeTopicIds: Uuid[]
  equippedTitleId?: Uuid | null
  activeMatchId?: Uuid | null
  inQueue: boolean
  createdAt: Iso
}

export interface RegisterRequest {
  email: string
  username: string
  password: string
}

export interface LoginRequest {
  email: string
  password: string
}

export interface RefreshRequest {
  refreshToken: string
}

// POST /api/auth/forgot-password → 202 always. The e-mail links to /reset-password?token=...
export interface ForgotPasswordRequest {
  email: string
}

// POST /api/auth/reset-password → 204, or 409 urn:app:auth/reset-token-invalid. Every session is logged out.
export interface ResetPasswordRequest {
  token: string
  newPassword: string
}

export interface AuthResponse {
  accessToken: string
  accessTokenExpiresAt: Iso
  refreshToken: string
  user: MeDto
}

export interface UpdateMeRequest {
  username?: string
  avatar?: string
  locale?: 'en' | 'hr'
  profileVisibility?: ProfileVisibility
  notifications?: Record<string, boolean>
}

export interface SetTopicsRequest {
  topicIds: Uuid[]
}

export interface DashboardTopicDto {
  topic: TopicDto
  active: boolean
  mastery?: number | null // average of answered concepts; null until any concept is answered
  completion: number // % of the topic's concepts graduated (0–100)
}

export interface DailyTaskDto {
  key: string
  progress: number
  target: number
  completed: boolean
  reward: number
  topic?: TopicDto | null
}

export interface DashboardDto {
  me: MeDto
  topics: DashboardTopicDto[]
  ratingHistory: { at: Iso; rating: number }[]
  dueReviews: number
  dailyTasks: DailyTaskDto[]
  dailyChallengeAttempted: boolean
  suggestion?: { kind: 'review_due' | 'practice_after_losses' | 'calibrate' | 'play'; topicId?: Uuid | null; conceptIds?: Uuid[] | null } | null
  inbox: {
    rematch?: { matchId: Uuid; from: UserRefDto; expiresAt: Iso } | null
    friendChallenge?: { code: string; host: UserRefDto; expiresAt: Iso } | null
  }
  queueLocked: boolean
}

// ---- Content and matches (F1, docs §6.1 and §6.5) ----

export type QuestionType =
  | 'true_false'
  | 'multiple_choice'
  | 'cloze'
  | 'odd_one_out'
  | 'output'
  | 'match_pairs'
  | 'order'
  | 'find_bug'
export type Difficulty = 1 | 2 | 3 | 4
export type AnswerLabel = 'brilliant' | 'good' | 'miss' | 'blunder'
export type ConceptState = 'unknown' | 'weak' | 'in_progress' | 'solid' | 'graduated'
export type MatchKind = 'ranked' | 'ghost' | 'bot' | 'friend' | 'practice' | 'calibration'
export type MatchStatus = 'veto' | 'lobby' | 'in_progress' | 'finished' | 'aborted'
export type MatchPhase = 'veto_coin' | 'veto_ban' | 'veto_pick' | 'lobby' | 'question' | 'interstitial' | 'done'
export type MatchResultReason = 'score' | 'tiebreak_correct' | 'tiebreak_time' | 'draw' | 'resign' | 'disconnect' | 'aborted'
export type MatchSlot = 'warmup' | 'targeted' | 'core' | 'review'
export type SeatKind = 'human' | 'bot' | 'ghost'
export type BotPersona = 'vibe_coder' | 'junior' | 'mid' | 'senior' // display names are localized on the client

export interface Option {
  id: string
  text: string
}

// Public part of a question (no key). Code is at most 25 lines of at most 60 characters; find_bug lines are 1-based.
export type QuestionContent =
  | { type: 'true_false'; statement: string }
  | { type: 'multiple_choice'; prompt: string; code?: string; options: Option[] }
  | { type: 'cloze'; prompt?: string; code: string; options: Option[] } // code contains exactly one "____"
  | { type: 'odd_one_out'; prompt: string; items: Option[] }
  | { type: 'output'; prompt?: string; code: string; options: Option[] }
  | { type: 'match_pairs'; prompt: string; left: Option[]; right: Option[] }
  | { type: 'order'; prompt: string; steps: Option[] }
  | { type: 'find_bug'; prompt: string; code: string; fixes: Option[] }

export type AnswerPayload =
  | { value: boolean } // true_false
  | { optionId: string } // multiple_choice, cloze, output
  | { itemId: string } // odd_one_out
  | { pairs: { leftId: string; rightId: string }[] } // match_pairs
  | { orderedIds: string[] } // order
  | { line: number; fixId: string } // find_bug

export interface QuestionPublicDto {
  id: Uuid
  type: QuestionType
  difficulty: Difficulty
  language?: string | null
  timeLimitMs: number
  basePoints: number
  content: QuestionContent
}

export interface AnswerKeyDto {
  // find_bug keys may add acceptLines: other lines of the same split statement that also count as the bug line.
  answer: AnswerPayload & { acceptLines?: number[] }
  remember: string
}

export interface AnswerResultDto {
  payload?: AnswerPayload | null // null when timed out
  isCorrect: boolean
  fraction: number // 0..1, partial credit for match_pairs and find_bug
  points: number
  elapsedMs: number
  timedOut: boolean
  label: AnswerLabel
}

// GET /api/questions/{id}/explanation (only after answering the question; 403 urn:app:question/not-answered otherwise).
// The first request may take up to 8 s while the AI writes it; after that it is cached for everyone.
// source 'static' = the AI was unavailable: whyCorrect and whyDistractor are empty, show only `remember`.
export interface ExplanationDto {
  whyCorrect: string
  whyDistractor: string
  remember: string
  source: 'ai' | 'static'
}

export interface PlayerDto {
  seat: 0 | 1
  kind: SeatKind
  user?: UserRefDto | null
  persona?: BotPersona | null
  ghostRecordedAt?: Iso | null
  displayRating: number // show this (not user.rating): a ghost plays with the rating it had when recorded
  score: number // only points already revealed in an interstitial
  connected: boolean
  hasAnswered: boolean // "Answered" indicator for the opponent; never the answer itself
}

export interface QuestionTurnDto {
  index: number
  total: number
  question: QuestionPublicDto
  startedAt: Iso
  deliveredAt?: Iso | null // when this client first received the question; after a reload, elapsed = serverTime - deliveredAt
  myAnswer?: { payload?: AnswerPayload | null; elapsedMs: number } | null
}

export interface InterstitialDto {
  index: number
  key: AnswerKeyDto
  mine: AnswerResultDto
  opponent?: AnswerResultDto | null
}

export interface ReviewQuestionDto {
  index: number
  question: QuestionPublicDto
  key: AnswerKeyDto
  concept: ConceptDto
  slot: MatchSlot
  mine: AnswerResultDto
  opponent?: AnswerResultDto | null
  explanation?: ExplanationDto | null
  reported: boolean
}

export interface MatchResultDto {
  winnerSeat?: 0 | 1 | null
  reason: MatchResultReason
  mine: {
    score: number
    correct: number
    ratingBefore?: number | null
    ratingAfter?: number | null
    sp: number
    coins: number
    xp: number
    streakSecured: boolean
  }
  opponent?: { score: number; correct: number } | null
  cards: {
    blunder?: ReviewQuestionDto | null
    brilliant?: ReviewQuestionDto | null
    practiceConcept?: { concept: ConceptDto; masteryDelta: number } | null
  }
  lessonAvailable: boolean
  lessonRewardEligible: boolean
  lessonId?: Uuid | null
  rematchAvailable: boolean
  reviewAvailable: boolean
}

// Poll GET /api/matches/{id} about once a second until status is finished or aborted.
// question: present in the question and interstitial phases; interstitial: only in interstitial; result: only when over.
// Countdowns use phaseDeadline - serverTime, never the device clock. phaseDeadline is null while untimed practice waits for "next".
export interface MatchStateDto {
  id: Uuid
  kind: MatchKind
  status: MatchStatus
  phase: MatchPhase
  serverTime: Iso
  phaseDeadline?: Iso | null
  topic?: TopicDto | null
  timed: boolean
  mySeat: 0 | 1
  players: PlayerDto[] // 2 in duels, 1 in practice
  veto?: VetoDto | null // ranked, only in veto_coin, veto_ban and veto_pick
  question?: QuestionTurnDto | null
  interstitial?: InterstitialDto | null
  result?: MatchResultDto | null
}

export interface StartBotMatchRequest {
  persona: BotPersona
  topicId: Uuid
}

export interface StartPracticeRequest {
  topicId: Uuid
  timed?: boolean // default true
  conceptIds?: Uuid[] // "Practice this", "Due for review": these concepts come first
}

export interface SubmitAnswerRequest {
  questionIndex: number
  payload: AnswerPayload
  clientElapsedMs: number // measured with performance.now() from the first render of the question
}

export interface MatchSummaryDto {
  id: Uuid
  kind: MatchKind
  topic?: TopicDto | null
  opponent?: { kind: SeatKind; persona?: BotPersona | null; user?: UserRefDto | null; displayRating: number } | null
  myScore: number
  opponentScore: number
  outcome: 'win' | 'loss' | 'draw' | 'aborted' | 'completed' // completed = single-player practice
  ratingDelta?: number | null
  finishedAt?: Iso | null
  lessonId?: Uuid | null
}

export interface BotDto {
  persona: BotPersona
  displayRating: number
  recommended: boolean
}

// ---- Ranked: veto and queue (F3, docs §6.5 and §6.6) ----

// veto_coin (3 s animation, coinWinnerSeat bans first) → veto_ban (8 s per turn; timeout = automatic random ban)
// → veto_pick (3 s animation towards chosenTopicId, already known) → lobby.
export interface VetoDto {
  pool: {
    topic: TopicDto
    myMastery?: number | null // my own mastery bar; the opponent's is never sent
    pickedByOpponent: boolean // icon: the opponent has this topic active
    bannedBySeat?: 0 | 1 | null
    auto?: boolean | null // banned automatically after the 8 s ran out
  }[]
  coinWinnerSeat: 0 | 1
  turnSeat?: 0 | 1 | null // who bans now; null once two topics remain
  bansRemaining: number
  finalTwo?: Uuid[] | null
  chosenTopicId?: Uuid | null
}

// POST /api/matches/{id}/veto/ban → MatchStateDto (409 urn:app:match/not-your-turn, urn:app:match/topic-not-in-pool, wrong-phase)
export interface BanTopicRequest {
  topicId: Uuid
}

export type QueueStatus = 'idle' | 'waiting' | 'proposed' | 'matched'

// POST /api/queue (join; 409 topics/count, queue/already-in, match/already-in-match), GET /api/queue (poll 1 s),
// DELETE /api/queue (leave; also declines an offer), POST /api/queue/proposals/{id}/accept (→ QueueStateDto).
export interface QueueStateDto {
  status: QueueStatus
  enteredAt?: Iso | null
  serverTime: Iso
  rangeNow?: number | null // ± rating range right now: 100, +50 every 10 s, at most 400
  topicIds: Uuid[] // locked while queued (PUT /api/me/topics → 409 topics/locked-in-queue)
  proposal?: { id: Uuid; opponent: UserRefDto; expiresAt: Iso; iAccepted: boolean } | null // 10 s to accept
  ghostOffer?: { opponent: UserRefDto; recordedAt: Iso; topic: TopicDto } | null // F3b, always null until then
  botOffer: boolean // after 30 s without an opponent; POST /api/matches/bot leaves the queue
  matchId?: Uuid | null // status 'matched': open this match
}

export interface StartCalibrationRequest {
  topicId: Uuid // 409 urn:app:match/nothing-to-calibrate when every concept already has answers
}

// ---- Analysis, lessons and progress (F2, docs §6.4, §6.5, §6.7) ----

// GET /api/matches/{id}/review (409 urn:app:match/not-finished while the match runs)
export interface MatchReviewDto {
  match: MatchSummaryDto
  questions: ReviewQuestionDto[] // explanation is set only once someone has opened it (fetch it with the explanation endpoint)
  masteryDelta: { concept: ConceptDto; before: number; after: number }[]
}

// GET /api/questions/{id}: key only when I have answered this question
export interface QuestionDetailDto extends QuestionPublicDto {
  key?: AnswerKeyDto | null
}

// POST /api/questions/{id}/report → 204 (once per player; the third player's report removes the question from new decks)
export interface ReportQuestionRequest {
  reason: string // 1–500 characters
}

export type LessonStatus = 'generated' | 'passed' | 'failed'

export interface LessonCardDto {
  conceptId: Uuid
  title: string
  why: string
  code?: string | null
  language?: string | null
  misconception: string // empty when the AI was unavailable and the card was built from the concept description
}

export interface LessonCheckResultDto {
  questionId: Uuid
  isCorrect: boolean
  key: AnswerKeyDto
  isVariantOfMissed: boolean
}

// POST /api/lessons { matchId } → 201 (409 urn:app:lesson/nothing-missed); GET /api/lessons/{id}
// One card per concept (up to 3 missed + up to 2 chronic gaps). check[0] is the variant of the missed question.
// After a failed check the next attempt asks fresh questions; it opens at retryAfter.
export interface LessonDto {
  id: Uuid
  matchId?: Uuid | null
  status: LessonStatus
  concepts: ConceptDto[]
  cards: LessonCardDto[]
  check: { question: QuestionPublicDto; isVariantOfMissed: boolean }[]
  attempt: number
  rewardEligible: boolean
  retryAfter?: Iso | null
  lastResults?: LessonCheckResultDto[] | null
}

// POST /api/lessons/{id}/check (409 urn:app:lesson/already-passed, urn:app:lesson/retry-too-soon). Untimed.
export interface SubmitLessonCheckRequest {
  answers: { questionId: Uuid; payload: AnswerPayload }[]
}

export interface LessonResultDto {
  passed: boolean // the variant right and at least one of the other two
  results: LessonCheckResultDto[]
  patchedConcepts: ConceptDto[] // all lesson concepts on a pass; they enter the review queue, due tomorrow
  rewards: { coins: number; sp: number; xp: number } // 0 until the economy (F4)
  retryAfter?: Iso | null
}

// GET /api/me/progress/due: most overdue first; practice them with POST /api/matches/practice { topicId, conceptIds }
export interface DueConceptsDto {
  concepts: (ConceptDto & { reviewDueAt: Iso; reviewStep: number; topic: TopicDto })[]
}

// GET /api/me/knowledge-map: every playable topic, concepts in display order.
// mastery: average of answered concepts (null until one is answered); completion: % of concepts graduated (0–100).
export interface KnowledgeMapDto {
  topics: {
    topic: TopicDto
    active: boolean
    mastery?: number | null
    completion: number
    calibrationAvailable: boolean // after the first answer in the topic, while some concept is still unknown
    concepts: {
      concept: ConceptDto
      mastery: number // 50 while unknown
      state: ConceptState
      reviewDueAt?: Iso | null
      reviewStep?: number | null
      lastAnswers: { questionId: Uuid; type: QuestionType; prompt: string; isCorrect: boolean; at: Iso }[] // newest first, up to 3
    }[]
  }[]
}
