import type {
  AppErrorCode,
  AuthResponse,
  DashboardDto,
  HealthResponse,
  LoginRequest,
  MeDto,
  ProblemDetails,
  RegisterRequest,
  SetTopicsRequest,
  TopicDto,
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
// localStorage for Capacitor Preferences (docs §6.10).
let accessToken: string | undefined
const REFRESH_TOKEN_KEY = 'refreshToken'

export const tokenStore = {
  get: async () => localStorage.getItem(REFRESH_TOKEN_KEY) ?? undefined,
  set: async (token: string) => localStorage.setItem(REFRESH_TOKEN_KEY, token),
  clear: async () => localStorage.removeItem(REFRESH_TOKEN_KEY),
}

let onUnauthenticated = () => {}

// Runs when a request gets 401 and the session cannot be refreshed; the app sends the user to /login.
export function setUnauthenticatedHandler(handler: () => void) {
  onUnauthenticated = handler
}

async function startSession(auth: AuthResponse) {
  accessToken = auth.accessToken
  await tokenStore.set(auth.refreshToken)
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
    try {
      return await startSession(await send<AuthResponse>('/api/auth/refresh', post({ refreshToken })))
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
    startSession(await request<AuthResponse>('/api/auth/register', post(body), { auth: false })),
  login: async (body: LoginRequest) =>
    startSession(await request<AuthResponse>('/api/auth/login', post(body), { auth: false })),
  logout: async () => {
    const refreshToken = await tokenStore.get()
    await endSession()
    // Fire and forget: the local session is already gone, so an unreachable API must not block logging out.
    if (refreshToken) request('/api/auth/logout', post({ refreshToken }), { auth: false }).catch(() => {})
  },

  me: (signal?: AbortSignal) => request<MeDto>('/api/me', { signal }),
  dashboard: (signal?: AbortSignal) => request<DashboardDto>('/api/me/dashboard', { signal }),
  setTopics: (body: SetTopicsRequest) =>
    request<void>('/api/me/topics', { method: 'PUT', body: JSON.stringify(body) }),
}
