import type { Messages } from '@/lib/i18n/en'
import type { BotPersona, Tier } from '@/types/api'

// Display constants from the game idea (§12 bots, §14 tiers) and GameRules. The server decides; these only draw it.
export const PLACEMENT_MATCHES = 10 // GameRules.ProvisionalMatches
export const MIN_TOPICS = 3
export const MAX_TOPICS = 5
export const QUESTIONS_PER_MATCH = 10 // GameRules.QuestionsPerMatch
export const LOBBY_SECONDS = 5 // GameRules.LobbySeconds: the look at both seats before question 1
export const DAY_TIME_ZONE = 'Europe/Zagreb' // GameRules.DayTimeZone: daily tasks and the challenge reset at its midnight
export const DAILY_BONUS = 30 // DailyService: all three daily tasks pay 30 coins more
export const DAILY_CHALLENGE_REWARD = { coins: 30, xp: 30 } // DailyService: for the right answer (5 coins for trying)

// The first 10 ranked duels are placement: the rating stays hidden and reads "Placement", as on the profile.
export const ratingText = (rating: number, provisional: boolean | undefined, t: Messages) =>
  provisional ? t.publicProfile.placement : String(rating)

const TIERS = ['bronze', 'silver', 'gold', 'platinum', 'diamond', 'master'] as const
// Rating where each tier starts. Bronze has no floor, so its meter starts at the start rating.
const TIER_FLOORS = [1000, 1100, 1300, 1500, 1700, 1900]

const clamp = (value: number) => Math.min(1, Math.max(0, value))

// Display tier for a rating; the server owns the real one (Grandmaster is the global top 100, not a rating).
export const tierFor = (rating: number): Tier => TIERS[Math.max(0, TIER_FLOORS.findLastIndex((floor) => rating >= floor))]

// Progress from the floor of the current tier to the next one. Master and Grandmaster have nowhere to go.
export function tierProgress(tier: Tier, rating: number): { next?: Tier; remaining: number; progress: number } {
  const index = TIERS.indexOf(tier as (typeof TIERS)[number])
  if (index < 0 || index === TIERS.length - 1) return { remaining: 0, progress: 1 }
  const floor = TIER_FLOORS[index]
  const target = TIER_FLOORS[index + 1]
  return { next: TIERS[index + 1], remaining: Math.max(0, target - rating), progress: clamp((rating - floor) / (target - floor)) }
}

// Level n + 1 needs 100 * (n + 1) XP on top of level n (App.Domain Level).
export function levelProgress(level: number, xpToNext: number) {
  const span = 100 * (level + 1)
  return clamp((span - xpToNext) / span)
}

// Shown before /api/bots answers (the dashboard hint); the play screen uses the list and recommendation from the API.
const BOT_PERSONAS: { persona: BotPersona; rating: number }[] = [
  { persona: 'vibe_coder', rating: 1000 },
  { persona: 'junior', rating: 1200 },
  { persona: 'mid', rating: 1500 },
  { persona: 'senior', rating: 1800 },
]

// The idea recommends the persona closest to the player's rating.
export const recommendedBot = (rating: number) =>
  BOT_PERSONAS.reduce((best, bot) => (Math.abs(bot.rating - rating) < Math.abs(best.rating - rating) ? bot : best))
