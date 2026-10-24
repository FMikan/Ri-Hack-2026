import type { Tier } from '@/types/api'

// Display constants from the game idea (§12 bots, §14 tiers) and GameRules. The server decides; these only draw it.
export const PLACEMENT_MATCHES = 10 // GameRules.ProvisionalMatches
export const MIN_TOPICS = 3
export const MAX_TOPICS = 5

const TIERS = ['bronze', 'silver', 'gold', 'platinum', 'diamond', 'master'] as const
// Rating where each tier starts. Bronze has no floor, so its meter starts at the start rating.
const TIER_FLOORS = [1000, 1100, 1300, 1500, 1700, 1900]

const clamp = (value: number) => Math.min(1, Math.max(0, value))

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

const BOT_PERSONAS = [
  { key: 'vince', rating: 1000 },
  { key: 'jake', rating: 1200 },
  { key: 'mike', rating: 1500 },
  { key: 'sarah', rating: 1800 },
] as const

// The idea recommends the persona closest to the player's rating.
export const recommendedBot = (rating: number) =>
  BOT_PERSONAS.reduce<(typeof BOT_PERSONAS)[number]>((best, bot) =>
    Math.abs(bot.rating - rating) < Math.abs(best.rating - rating) ? bot : best,
  BOT_PERSONAS[0])
