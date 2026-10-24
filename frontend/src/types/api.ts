// Mirrors the backend contracts (App.Api / App.Application). Keep in sync when endpoints change.

export type HealthStatus = 'Healthy' | 'Degraded' | 'Unhealthy'

export interface HealthResponse {
  status: HealthStatus
  checks: Record<string, HealthStatus>
}

export interface Note {
  id: string
  text: string
  createdAt: string
}

export interface CreateNoteRequest {
  text: string
}

// RFC 9457 Problem Details, returned by the API for every error response.
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  errors?: Record<string, string[]>
}
