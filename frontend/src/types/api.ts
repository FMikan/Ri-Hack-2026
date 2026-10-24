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
  completion: number
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
