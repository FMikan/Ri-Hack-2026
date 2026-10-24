import type { DashboardDto, MeDto } from '@/types/api'

// Test numbers for the dashboard until the backend phases behind them exist (ranked matches F3, mastery F2,
// economy F4). Opt in with VITE_DEMO_DATA=true in frontend/.env.local; the top bar then shows a "Demo data" badge.
// Only stats are faked: topics, the username and everything the player can change stay real.
export const DEMO_DATA = import.meta.env.VITE_DEMO_DATA === 'true'

const RATINGS = [
  1000, 1014, 1031, 1022, 1046, 1063, 1055, 1078, 1094, 1112, 1101, 1127, 1149, 1138, 1166, 1189, 1214, 1203, 1241, 1262,
  1287,
]
const MASTERY: Record<string, number> = {
  javascript: 78,
  frontend: 64,
  backend: 52,
  sql: 85,
  networking: 41,
  security: 33,
  algorithms: 57,
  ai: 69,
}
const DAY = 86_400_000

export function demoMe(me: MeDto): MeDto {
  if (!DEMO_DATA) return me
  return {
    ...me,
    rating: RATINGS[RATINGS.length - 1],
    tier: 'silver', // 1287 is silver; gold starts at 1300 (game idea §14)
    isProvisional: false,
    rankedMatches: 23,
    xp: 1340, // App.Domain Level: level 4 starts at 900 XP, level 5 at 1400
    level: 4,
    xpToNextLevel: 60,
    coins: 840,
    seasonPoints: 120,
    streak: { count: 6, lastActiveOn: new Date().toLocaleDateString('sv-SE'), freezes: 1, securedToday: true },
  }
}

export function demoDashboard(dashboard: DashboardDto): DashboardDto {
  if (!DEMO_DATA) return dashboard
  const now = Date.now()
  return {
    ...dashboard,
    me: demoMe(dashboard.me),
    topics: dashboard.topics.map((entry, index) => ({
      ...entry,
      mastery: entry.mastery ?? MASTERY[entry.topic.slug] ?? 35 + ((index * 23) % 50),
    })),
    ratingHistory: RATINGS.map((rating, index) => ({
      at: new Date(now - (RATINGS.length - 1 - index) * DAY).toISOString(),
      rating,
    })),
    dueReviews: 4,
  }
}
