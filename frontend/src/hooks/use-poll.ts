import { useCallback, useEffect, useRef, useState } from 'react'

interface PollControls<T> {
  run: () => void
  replace: (data: T) => void
}

// Runs `load` every `intervalMs` until `until(data)` holds (docs §6.10). Keeps the last data, so there is no loading
// flicker after the first response; pauses while the tab is hidden and refetches as soon as it is visible again.
// `replace` swaps in data a command returned and drops any poll still in flight, so an older response cannot win.
// `load` and `until` must be stable (module-level or useCallback).
export function usePoll<T>(load: (signal: AbortSignal) => Promise<T>, intervalMs: number, until: (data: T) => boolean) {
  const [state, setState] = useState<{ data?: T; error?: Error }>({})
  const controls = useRef<PollControls<T>>(undefined)

  useEffect(() => {
    let controller: AbortController | undefined
    let timer: number | undefined
    let done = false

    const schedule = () => {
      window.clearTimeout(timer)
      if (!done && !document.hidden) timer = window.setTimeout(run, intervalMs)
    }
    const accept = (data: T) => {
      done = until(data)
      setState({ data })
      schedule()
    }
    async function run() {
      window.clearTimeout(timer)
      controller?.abort()
      const current = (controller = new AbortController())
      try {
        const data = await load(current.signal)
        if (!current.signal.aborted) accept(data)
      } catch (error) {
        if (current.signal.aborted) return
        setState((previous) => ({ ...previous, error: error instanceof Error ? error : new Error(String(error)) }))
        schedule()
      }
    }
    const onVisibility = () => (document.hidden ? window.clearTimeout(timer) : !done && run())

    controls.current = {
      run,
      replace: (data) => {
        controller?.abort()
        accept(data)
      },
    }
    run()
    document.addEventListener('visibilitychange', onVisibility)
    return () => {
      done = true
      controller?.abort()
      window.clearTimeout(timer)
      document.removeEventListener('visibilitychange', onVisibility)
    }
  }, [load, intervalMs, until])

  const replace = useCallback((data: T) => controls.current?.replace(data), [])
  const refresh = useCallback(() => controls.current?.run(), [])
  return { ...state, replace, refresh }
}
