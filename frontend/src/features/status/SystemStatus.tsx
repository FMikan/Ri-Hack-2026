import { RefreshCw } from 'lucide-react'
import { StatusIndicator, type StatusTone } from '@/components/shared/StatusIndicator'
import { Button } from '@/components/ui/button'
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { useAsync } from '@/hooks/use-async'
import { api, ApiError } from '@/lib/api-client'

interface Row {
  name: string
  endpoint: string
  tone: StatusTone
  label: string
  detail: string
}

export function SystemStatus() {
  const live = useAsync(api.health)
  const ready = useAsync(api.readiness)

  const apiRow: Row = live.loading
    ? { name: 'API', endpoint: 'GET /health', tone: 'pending', label: 'Checking', detail: 'Contacting the backend.' }
    : live.error
      ? { name: 'API', endpoint: 'GET /health', tone: 'error', label: 'Offline', detail: live.error.message }
      : { name: 'API', endpoint: 'GET /health', tone: 'ok', label: 'Online', detail: 'Backend process is running.' }

  const databaseDown = ready.error instanceof ApiError && ready.error.status === 503
  const dbRow: Row = ready.loading
    ? { name: 'Database', endpoint: 'GET /health/ready', tone: 'pending', label: 'Checking', detail: 'Asking the API to reach PostgreSQL.' }
    : databaseDown
      ? { name: 'Database', endpoint: 'GET /health/ready', tone: 'error', label: 'Unavailable', detail: 'The API is up but cannot reach PostgreSQL. Is the container running?' }
      : ready.error
        ? { name: 'Database', endpoint: 'GET /health/ready', tone: 'error', label: 'Unknown', detail: 'Readiness could not be checked because the API is unreachable.' }
        : { name: 'Database', endpoint: 'GET /health/ready', tone: 'ok', label: 'Connected', detail: 'PostgreSQL accepts connections.' }

  const recheck = () => {
    live.reload()
    ready.reload()
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <h2>System status</h2>
        </CardTitle>
        <CardDescription>Liveness and readiness checks from the backend.</CardDescription>
        <CardAction>
          <Button variant="outline" size="sm" onClick={recheck}>
            <RefreshCw aria-hidden="true" />
            Recheck
          </Button>
        </CardAction>
      </CardHeader>
      <CardContent>
        <dl className="divide-y divide-border" aria-live="polite">
          {[apiRow, dbRow].map((row) => (
            <div key={row.name} className="grid grid-cols-[1fr_auto] items-center gap-x-4 gap-y-1 py-3 first:pt-0 last:pb-0">
              <dt className="font-medium">
                {row.name} <span className="ml-1 font-mono text-xs text-muted-foreground">{row.endpoint}</span>
              </dt>
              <dd>
                <StatusIndicator tone={row.tone} label={row.label} />
              </dd>
              <dd className="col-span-2 text-muted-foreground">{row.detail}</dd>
            </div>
          ))}
        </dl>
      </CardContent>
    </Card>
  )
}
