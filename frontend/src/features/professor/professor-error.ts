import { ApiError } from '@/lib/api-client'
import type { Messages } from '@/lib/i18n/en'

// The professor's own failures in the player's language; anything else (429, validation) shows the server's detail.
export function professorError(caught: unknown, t: Messages) {
  if (!(caught instanceof ApiError)) return t.professor.failed
  if (caught.status === 0) return t.common.serverUnreachable
  switch (caught.code) {
    case 'urn:app:professor/unavailable':
      return t.professor.unavailable
    case 'urn:app:professor/failed':
      return t.professor.failed
    case 'urn:app:professor/declined':
      return t.professor.declined
    default:
      return caught.message
  }
}
