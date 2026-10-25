import type {
  AchievementDto,
  AppErrorCode,
  AuthConfigDto,
  AuthResponse,
  BanTopicRequest,
  Board,
  BotDto,
  CreateFriendChallengeRequest,
  DailyAttemptRequest,
  DailyChallengeDto,
  DashboardDto,
  DueConceptsDto,
  ExplanationDto,
  ForgotPasswordRequest,
  FriendChallengeCreatedDto,
  FriendChallengeDto,
  FriendDto,
  FriendRequest,
  GoogleLoginRequest,
  KnowledgeMapDto,
  LeaderboardDto,
  LessonDto,
  LessonResultDto,
  HealthResponse,
  LoginRequest,
  MatchReviewDto,
  MatchStateDto,
  MatchSummaryDto,
  MeDto,
  ProblemDetails,
  PublicProfileDto,
  PurchaseRequest,
  QuestionDetailDto,
  QueueStateDto,
  RegisterRequest,
  RewardEventDto,
  SeasonDto,
  ReportQuestionRequest,
  ResetPasswordRequest,
  RematchDto,
  SetEquipmentRequest,
  SetTopicsRequest,
  ShopDto,
  ShopItemDto,
  StatsDto,
  StartBotMatchRequest,
  StartCalibrationRequest,
  StartPracticeRequest,
  SubmitAnswerRequest,
  SubmitLessonCheckRequest,
  TitleDto,
  TopicDto,
  UpdateMeRequest,
} from '@/types/api'

// The client always calls the API by its full origin (no Vite proxy); the backend allows this origin via CORS.
export const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5080').replace(/\/$/, '')

export class ApiError extends Error {
  readonly status: number
  readonly problem?: ProblemDetails

  // status 0 means the request never got a response (API down, wrong URL, CORS).
  constructor(status: number, problem?: ProblemDetails) {
    const firstValidationError = Object.values(problem?.errors ?? {})[0]?.[0]
    super(
      firstValidationError ??
        problem?.detail ??
        problem?.title ??
        (status === 0 ? `Cannot reach the API at ${API_BASE_URL}` : `Request failed (${status})`),
    )
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }

  // Game-rule and auth errors carry type "urn:app:<code>" (docs §6); branch on this, not on the message.
  get code(): AppErrorCode | undefined {
    return this.problem?.type?.startsWith('urn:app:') ? (this.problem.type as AppErrorCode) : undefined
  }
}

// Access token lives only in memory. The refresh token sits behind an async store so the Android build can swap
// localStorage for Capacitor Preferences (docs §6.10). "Remember me" keeps it in localStorage; without it the token
// goes to sessionStorage and the session ends when the browser closes. A rotated token stays where the old one was.
let accessToken: string | undefined
const REFRESH_TOKEN_KEY = 'refreshToken'

export const tokenStore = {
  get: async () => localStorage.getItem(REFRESH_TOKEN_KEY) ?? sessionStorage.getItem(REFRESH_TOKEN_KEY) ?? undefined,
  remembered: async () => localStorage.getItem(REFRESH_TOKEN_KEY) !== null,
  set: async (token: string, remember: boolean) => {
    await tokenStore.clear()
    ;(remember ? localStorage : sessionStorage).setItem(REFRESH_TOKEN_KEY, token)
  },
  clear: async () => {
    localStorage.removeItem(REFRESH_TOKEN_KEY)
    sessionStorage.removeItem(REFRESH_TOKEN_KEY)
  },
}

let onUnauthenticated = () => {}

// Runs when a request gets 401 and the session cannot be refreshed; the app sends the user to /login.
export function setUnauthenticatedHandler(handler: () => void) {
  onUnauthenticated = handler
}

async function startSession(auth: AuthResponse, remember: boolean) {
  accessToken = auth.accessToken
  await tokenStore.set(auth.refreshToken, remember)
  return auth
}

async function endSession() {
  accessToken = undefined
  await tokenStore.clear()
}

async function send<T>(path: string, init: RequestInit, token?: string): Promise<T> {
  let response: Response
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      ...init,
      headers: {
        Accept: 'application/json',
        ...(init.body ? { 'Content-Type': 'application/json' } : {}),
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
        ...init.headers,
      },
    })
  } catch (error) {
    if (init.signal?.aborted) throw error
    throw new ApiError(0)
  }

  const body: unknown = await response.json().catch(() => undefined)
  if (!response.ok) throw new ApiError(response.status, body as ProblemDetails | undefined)
  return body as T
}

const post = (body: unknown): RequestInit => ({ method: 'POST', body: JSON.stringify(body) })

let refreshing: Promise<AuthResponse | undefined> | undefined

// Resolves undefined when there is no session to restore. One refresh is shared by every caller: the server
// revokes the whole session when a refresh token is presented twice (StrictMode and parallel 401s would do that).
export function refreshSession(): Promise<AuthResponse | undefined> {
  refreshing ??= (async () => {
    const refreshToken = await tokenStore.get()
    if (!refreshToken) return undefined
    const remember = await tokenStore.remembered()
    try {
      return await startSession(await send<AuthResponse>('/api/auth/refresh', post({ refreshToken })), remember)
    } catch (error) {
      // Network errors keep the stored token so the session survives a brief outage.
      if (!(error instanceof ApiError && error.status === 401)) throw error
      await endSession()
      return undefined
    }
  })().finally(() => {
    refreshing = undefined
  })
  return refreshing
}

// Authenticated by default. On 401 the session is refreshed once and the request retried once.
// `/api/auth/*` and other public routes pass { auth: false } and are never retried.
async function request<T>(path: string, init: RequestInit = {}, { auth = true } = {}): Promise<T> {
  if (!auth) return send<T>(path, init)
  try {
    return await send<T>(path, init, accessToken)
  } catch (error) {
    if (!(error instanceof ApiError && error.status === 401)) throw error
    const session = await refreshSession()
    if (!session) {
      accessToken = undefined
      onUnauthenticated()
      throw error
    }
    return send<T>(path, init, session.accessToken)
  }
}

export const api = {
  health: (signal?: AbortSignal) => request<HealthResponse>('/health', { signal }, { auth: false }),
  // Responds 503 (thrown as ApiError) when the API is up but PostgreSQL is not reachable.
  readiness: (signal?: AbortSignal) => request<HealthResponse>('/health/ready', { signal }, { auth: false }),
  listTopics: (signal?: AbortSignal) => request<TopicDto[]>('/api/topics', { signal }, { auth: false }),

  register: async (body: RegisterRequest) =>
    startSession(await request<AuthResponse>('/api/auth/register', post(body), { auth: false }), true),
  login: async (body: LoginRequest, remember: boolean) =>
    startSession(await request<AuthResponse>('/api/auth/login', post(body), { auth: false }), remember),
  // The ID token from Google's button; the first Google sign-in creates the account.
  googleLogin: async (body: GoogleLoginRequest, remember: boolean) =>
    startSession(await request<AuthResponse>('/api/auth/google', post(body), { auth: false }), remember),
  // googleClientId is null when Google sign-in is off.
  authConfig: (signal?: AbortSignal) => request<AuthConfigDto>('/api/auth/config', { signal }, { auth: false }),
  logout: async () => {
    const refreshToken = await tokenStore.get()
    await endSession()
    // Fire and forget: the local session is already gone, so an unreachable API must not block logging out.
    if (refreshToken) request('/api/auth/logout', post({ refreshToken }), { auth: false }).catch(() => {})
  },

  // Always 202, whether or not the account exists; the link in the e-mail opens /reset-password?token=...
  forgotPassword: (body: ForgotPasswordRequest) => request<void>('/api/auth/forgot-password', post(body), { auth: false }),
  resetPassword: (body: ResetPasswordRequest) => request<void>('/api/auth/reset-password', post(body), { auth: false }),

  me: (signal?: AbortSignal) => request<MeDto>('/api/me', { signal }),
  updateMe: (body: UpdateMeRequest) => request<MeDto>('/api/me', { method: 'PATCH', body: JSON.stringify(body) }),
  dashboard: (signal?: AbortSignal) => request<DashboardDto>('/api/me/dashboard', { signal }),
  setTopics: (body: SetTopicsRequest) =>
    request<void>('/api/me/topics', { method: 'PUT', body: JSON.stringify(body) }),

  bots: (signal?: AbortSignal) => request<BotDto[]>('/api/bots', { signal }),
  startBotMatch: (body: StartBotMatchRequest) => request<MatchStateDto>('/api/matches/bot', post(body)),
  // Every match command returns the same state as the poll, so the screen replaces its state with the response.
  match: (id: string, signal?: AbortSignal) => request<MatchStateDto>(`/api/matches/${id}`, { signal }),
  answer: (id: string, body: SubmitAnswerRequest) => request<MatchStateDto>(`/api/matches/${id}/answers`, post(body)),
  resign: (id: string) => request<MatchStateDto>(`/api/matches/${id}/resign`, { method: 'POST' }),
  startPractice: (body: StartPracticeRequest) => request<MatchStateDto>('/api/matches/practice', post(body)),
  startCalibration: (body: StartCalibrationRequest) => request<MatchStateDto>('/api/matches/calibration', post(body)),
  // Untimed practice waits in the interstitial until the player moves on.
  next: (id: string) => request<MatchStateDto>(`/api/matches/${id}/next`, { method: 'POST' }),
  // Only after answering; the first request may take up to 8 s while the AI writes it.
  explanation: (questionId: string, signal?: AbortSignal) =>
    request<ExplanationDto>(`/api/questions/${questionId}/explanation`, { signal }),

  ban: (id: string, body: BanTopicRequest) => request<MatchStateDto>(`/api/matches/${id}/veto/ban`, post(body)),
  rematch: (id: string, signal?: AbortSignal) => request<RematchDto>(`/api/matches/${id}/rematch`, { signal }),
  // Asking while the opponent's request waits starts the rematch at once (status accepted + matchId).
  requestRematch: (id: string) => request<RematchDto>(`/api/matches/${id}/rematch`, { method: 'POST' }),
  acceptRematch: (id: string) => request<MatchStateDto>(`/api/matches/${id}/rematch/accept`, { method: 'POST' }),

  // Ranked queue (F3): join, poll once a second, accept the proposal; leaving also declines a pending proposal.
  queue: (signal?: AbortSignal) => request<QueueStateDto>('/api/queue', { signal }),
  joinQueue: () => request<QueueStateDto>('/api/queue', { method: 'POST' }),
  leaveQueue: () => request<void>('/api/queue', { method: 'DELETE' }),
  acceptProposal: (id: string) => request<QueueStateDto>(`/api/queue/proposals/${id}/accept`, { method: 'POST' }),
  playGhost: () => request<MatchStateDto>('/api/queue/ghost', { method: 'POST' }),
  declineGhost: () => request<QueueStateDto>('/api/queue/ghost/decline', { method: 'POST' }),

  review: (id: string, signal?: AbortSignal) => request<MatchReviewDto>(`/api/matches/${id}/review`, { signal }),
  // With the key only once I have answered the question.
  question: (id: string, signal?: AbortSignal) => request<QuestionDetailDto>(`/api/questions/${id}`, { signal }),
  reportQuestion: (id: string, body: ReportQuestionRequest) => request<void>(`/api/questions/${id}/report`, post(body)),
  startLesson: (matchId: string) => request<LessonDto>('/api/lessons', post({ matchId })),
  lesson: (id: string, signal?: AbortSignal) => request<LessonDto>(`/api/lessons/${id}`, { signal }),
  checkLesson: (id: string, body: SubmitLessonCheckRequest) => request<LessonResultDto>(`/api/lessons/${id}/check`, post(body)),

  // Economy (F4): purchases and equipment answer with the updated MeDto.
  shop: (signal?: AbortSignal) => request<ShopDto>('/api/shop', { signal }),
  purchase: (body: PurchaseRequest) => request<MeDto>('/api/shop/purchase', post(body)),
  myItems: (signal?: AbortSignal) => request<ShopItemDto[]>('/api/me/items', { signal }),
  equip: (body: SetEquipmentRequest) => request<MeDto>('/api/me/equipment', { method: 'PUT', body: JSON.stringify(body) }),
  rewards: (signal?: AbortSignal) => request<RewardEventDto[]>('/api/me/rewards?limit=50', { signal }),
  // Opens today's attempt: the 60 s run from the first call. The archive (by day) never opens one.
  dailyChallenge: (signal?: AbortSignal) => request<DailyChallengeDto>('/api/daily-challenge', { signal }),
  dailyChallengeOn: (day: string, signal?: AbortSignal) => request<DailyChallengeDto>(`/api/daily-challenge/${day}`, { signal }),
  dailyAttempt: (body: DailyAttemptRequest) => request<DailyChallengeDto>('/api/daily-challenge/attempt', post(body)),

  // Social and leaderboards (F5).
  leaderboard: (board: Board, topic?: string, signal?: AbortSignal) =>
    request<LeaderboardDto>(`/api/leaderboards/${board}${topic ? `?topic=${encodeURIComponent(topic)}` : ''}`, { signal }),
  season: (signal?: AbortSignal) => request<SeasonDto>('/api/seasons/current', { signal }),
  stats: (signal?: AbortSignal) => request<StatsDto>('/api/me/stats', { signal }),
  // Loading the list is also when new achievements are recorded.
  achievements: (signal?: AbortSignal) => request<AchievementDto[]>('/api/me/achievements', { signal }),
  titles: (signal?: AbortSignal) => request<TitleDto[]>('/api/me/titles', { signal }),
  publicProfile: (username: string, signal?: AbortSignal) =>
    request<PublicProfileDto>(`/api/users/${encodeURIComponent(username)}`, { signal }),
  friends: (signal?: AbortSignal) => request<FriendDto[]>('/api/friends', { signal }),
  // Asking someone who already asked me accepts their request.
  addFriend: (body: FriendRequest) => request<FriendDto>('/api/friends/requests', post(body)),
  acceptFriend: (userId: string) => request<FriendDto>(`/api/friends/requests/${userId}/accept`, { method: 'POST' }),
  // Unfriend, withdraw my request or decline theirs.
  removeFriend: (userId: string) => request<void>(`/api/friends/${userId}`, { method: 'DELETE' }),
  createChallenge: (body: CreateFriendChallengeRequest) => request<FriendChallengeCreatedDto>('/api/friend-challenges', post(body)),
  challenge: (code: string, signal?: AbortSignal) =>
    request<FriendChallengeDto>(`/api/friend-challenges/${encodeURIComponent(code)}`, { signal }),
  joinChallenge: (code: string) => request<MatchStateDto>(`/api/friend-challenges/${encodeURIComponent(code)}/join`, { method: 'POST' }),

  myMatches: (signal?: AbortSignal) => request<MatchSummaryDto[]>('/api/me/matches?limit=50', { signal }),
  dueConcepts: (signal?: AbortSignal) => request<DueConceptsDto>('/api/me/progress/due', { signal }),
  knowledgeMap: (signal?: AbortSignal) => request<KnowledgeMapDto>('/api/me/knowledge-map', { signal }),
}
