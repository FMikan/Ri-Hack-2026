import type { MatchStateDto } from '@/types/api'

// A poll response plus when it arrived, so countdowns run on (phaseDeadline - serverTime), never the device clock.
export interface Snapshot {
  match: MatchStateDto
  at: number
}

// Runs a match command and swaps in the state it returns; false when it failed with an error worth showing.
export type Command = (run: () => Promise<MatchStateDto>) => Promise<boolean>

export const msToDeadline = ({ match, at }: Snapshot, now: number) =>
  match.phaseDeadline ? Date.parse(match.phaseDeadline) - Date.parse(match.serverTime) - (now - at) : Infinity
