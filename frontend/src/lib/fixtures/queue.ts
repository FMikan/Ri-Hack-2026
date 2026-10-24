import type { Iso, Uuid } from '@/types/api'

// Ranked matchmaking is F3 and the backend has no queue yet. The play dialog runs this local stand-in, shaped like
// QueueStateDto (docs §6.6): it widens the rating range like the game idea (§4) and offers a bot after 30 s, but it
// never finds a human. Replace it with POST, GET and DELETE /api/queue (polled once a second) when they land.
export interface QueueStateDto {
  status: 'idle' | 'waiting' | 'proposed' | 'matched'
  enteredAt?: Iso | null
  serverTime: Iso
  rangeNow?: number | null
  topicIds: Uuid[]
  botOffer: boolean // after 30 s without a pair; POST /api/matches/bot leaves the queue
}

const START_RANGE = 100
const RANGE_STEP = 50 // every 10 s
const MAX_RANGE = 400
const BOT_OFFER_MS = 30_000

export function demoQueue(waitedMs: number, topicIds: Uuid[], now = Date.now()): QueueStateDto {
  return {
    status: 'waiting',
    enteredAt: new Date(now - waitedMs).toISOString(),
    serverTime: new Date(now).toISOString(),
    rangeNow: Math.min(MAX_RANGE, START_RANGE + RANGE_STEP * Math.floor(waitedMs / 10_000)),
    topicIds,
    botOffer: waitedMs >= BOT_OFFER_MS,
  }
}
