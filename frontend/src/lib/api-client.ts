import type { HealthResponse, ProblemDetails, TopicDto } from '@/types/api'

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
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  let response: Response
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      ...init,
      headers: {
        Accept: 'application/json',
        ...(init.body ? { 'Content-Type': 'application/json' } : {}),
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

export const api = {
  health: (signal?: AbortSignal) => request<HealthResponse>('/health', { signal }),
  // Responds 503 (thrown as ApiError) when the API is up but PostgreSQL is not reachable.
  readiness: (signal?: AbortSignal) => request<HealthResponse>('/health/ready', { signal }),
  // ponytail: authenticated calls (me, dashboard, topics selection) arrive with the token store and refresh flow (docs §6.10).
  listTopics: (signal?: AbortSignal) => request<TopicDto[]>('/api/topics', { signal }),
}
