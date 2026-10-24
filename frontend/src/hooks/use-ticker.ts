import { useEffect, useState } from 'react'

// Re-renders every `intervalMs` while active and returns performance.now(), for countdowns and elapsed timers.
export function useTicker(active: boolean, intervalMs = 100) {
  const [now, setNow] = useState(() => performance.now())
  useEffect(() => {
    if (!active) return
    const id = window.setInterval(() => setNow(performance.now()), intervalMs)
    return () => window.clearInterval(id)
  }, [active, intervalMs])
  return now
}
