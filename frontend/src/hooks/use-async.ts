import { useCallback, useEffect, useState } from 'react'

export interface AsyncState<T> {
  data?: T
  error?: Error
  loading: boolean
}

// Runs `load` on mount and on every reload(). Keeps the last data while reloading.
// `load` must be stable (a module-level function or wrapped in useCallback).
export function useAsync<T>(load: (signal: AbortSignal) => Promise<T>) {
  const [state, setState] = useState<AsyncState<T>>({ loading: true })
  const [version, setVersion] = useState(0)

  useEffect(() => {
    const controller = new AbortController()
    load(controller.signal).then(
      (data) => {
        if (!controller.signal.aborted) setState({ data, loading: false })
      },
      (error: unknown) => {
        if (!controller.signal.aborted)
          setState({ error: error instanceof Error ? error : new Error(String(error)), loading: false })
      },
    )
    return () => controller.abort()
  }, [load, version])

  const reload = useCallback(() => {
    setState((previous) => ({ ...previous, loading: true }))
    setVersion((v) => v + 1)
  }, [])
  return { ...state, reload }
}
