import { recommendedBot } from '@/lib/game'
import type { BotDto, DashboardDto, MeDto } from '@/types/api'

// Test numbers for the dashboard until the backend phases behind them exist (ranked matches F3, mastery F2,
// economy F4). Opt in with VITE_DEMO_DATA=true in frontend/.env.local.
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
  'how-ai-works': 69,
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

// Topics nobody has answered yet get a made-up mastery, so the skill graph has a shape. Shared by dashboard and profile.
export function demoTopics<T extends { topic: { slug: string }; mastery?: number | null }>(topics: T[]): T[] {
  if (!DEMO_DATA) return topics
  return topics.map((entry, index) => ({ ...entry, mastery: entry.mastery ?? MASTERY[entry.topic.slug] ?? 35 + ((index * 23) % 50) }))
}

export function demoDashboard(dashboard: DashboardDto): DashboardDto {
  if (!DEMO_DATA) return dashboard
  const now = Date.now()
  return {
    ...dashboard,
    me: demoMe(dashboard.me),
    topics: demoTopics(dashboard.topics),
    ratingHistory: RATINGS.map((rating, index) => ({
      at: new Date(now - (RATINGS.length - 1 - index) * DAY).toISOString(),
      rating,
    })),
    dueReviews: 4,
  }
}

// The server recommends a bot by the real rating; with demo stats the shown rating differs, so the recommendation follows
// the shown one too and matches the dashboard's suggestion.
export function demoBots(bots: BotDto[], me: MeDto): BotDto[] {
  if (!DEMO_DATA) return bots
  const { persona } = recommendedBot(demoMe(me).rating)
  return bots.map((bot) => ({ ...bot, recommended: bot.persona === persona }))
}
