import { tierFor } from '@/lib/game'
import type { Iso, MeDto, UserRefDto } from '@/types/api'

// Leaderboards are F5 and the backend does not serve them yet; the game idea (§14) fills them with mock players for
// the hackathon. Shapes mirror docs §6.9: move these types to src/types/api.ts and the call to api-client.ts when
// GET /api/leaderboards/{board} lands.
export type LeaderboardBoard = 'global' | 'season' | 'weekly' | 'topic' | 'friends'

export interface LeaderboardEntryDto {
  rank: number
  user: UserRefDto
  value: number // Elo | SP | weekly points | graduated concepts
}

export interface LeaderboardDto {
  board: LeaderboardBoard
  entries: LeaderboardEntryDto[]
  me?: { entry: LeaderboardEntryDto; around: LeaderboardEntryDto[] } | null
  resetsAt?: Iso | null
}

const NAMES = [
  'segfault_sam', 'async_ana', 'nullpointer', 'lambda_lea', 'regex_rick', 'kernel_kim', 'bytewise', 'deadlock_dan',
  'heap_hana', 'monad_mia', 'tcp_toni', 'sql_sara', 'hash_hugo', 'promise_petra', 'git_gita', 'cache_luka',
  'docker_dora', 'rust_rok', 'vim_vesna', 'pixel_pavle', 'json_jana', 'linux_lovro', 'bitwise_bruno', 'cron_cvita',
]
const FRIENDS = ['async_ana', 'cache_luka', 'git_gita', 'tcp_toni', 'json_jana']

// Small deterministic generator so every board looks the same on every visit.
function random(seed: string) {
  let state = [...seed].reduce((hash, char) => (hash * 31 + char.charCodeAt(0)) | 0, 7)
  return () => {
    state = (state * 1_103_515_245 + 12_345) | 0
    return ((state >>> 16) & 0x7fff) / 0x8000
  }
}

const user = (username: string, rating: number, grandmaster = false): UserRefDto => ({
  id: `demo-${username}`,
  username,
  avatar: 'default',
  tier: grandmaster ? 'grandmaster' : tierFor(rating),
  rating,
  isProvisional: false,
  equipment: {},
})

const nextMonday = (now: Date) => {
  const date = new Date(now)
  date.setDate(date.getDate() + ((8 - date.getDay()) % 7 || 7))
  date.setHours(0, 0, 0, 0)
  return date.toISOString()
}

export function demoLeaderboard(board: LeaderboardBoard, me: MeDto, topicSlug = ''): LeaderboardDto {
  const next = random(`${board}:${topicSlug}`)
  const ratings = new Map(NAMES.map((name, index) => [name, 2380 - index * 38 - Math.round(next() * 30)]))
  // What each board ranks by, highest first.
  const top = {
    global: { max: 2400, step: 22 },
    season: { max: 3120, step: 105 },
    weekly: { max: 2460, step: 88 },
    topic: { max: 12, step: 0.45 },
    friends: { max: 0, step: 0 },
  }[board]
  const pool = board === 'friends' ? FRIENDS : board === 'global' ? NAMES : [...NAMES].sort(() => next() - 0.5)
  const myValue = { global: me.rating, season: me.seasonPoints, weekly: 340, topic: 3, friends: me.rating }[board]

  const entries: LeaderboardEntryDto[] = pool.map((name, index) => {
    const rating = ratings.get(name)!
    const value =
      board === 'friends' ? rating : Math.max(0, Math.round(top.max - index * top.step - next() * top.step * 0.6))
    return { rank: index + 1, user: user(name, board === 'global' ? value : rating, ratings.get(name)! > 2250), value }
  })

  if (board === 'friends') {
    // A handful of friends with me among them, ranked by rating.
    const all = [...entries, { rank: 0, user: me, value: myValue }].sort((a, b) => b.value - a.value)
    const ranked = all.map((entry, index) => ({ ...entry, rank: index + 1 }))
    return { board, entries: ranked, me: { entry: ranked.find((entry) => entry.user.id === me.id)!, around: [] } }
  }

  // Further down the list: my own row with two players above and two below (§14).
  const myRank = { global: 1284, season: 412, weekly: 167, topic: 58, friends: 0 }[board]
  const around = [-2, -1, 0, 1, 2].map((offset) =>
    offset === 0
      ? { rank: myRank, user: me as UserRefDto, value: myValue }
      : {
          rank: myRank + offset,
          user: user(NAMES[(myRank + offset + 5) % NAMES.length].replace(/_/, '.'), me.rating - offset * 4),
          value: Math.max(0, myValue - offset * (board === 'topic' ? 0 : 3 + Math.round(next() * 6))),
        },
  )
  const resetsAt = board === 'weekly' ? nextMonday(new Date()) : board === 'season' ? new Date(Date.now() + 41 * 86_400_000).toISOString() : null
  return { board, entries: entries.slice(0, 20), me: { entry: around[2], around }, resetsAt }
}
