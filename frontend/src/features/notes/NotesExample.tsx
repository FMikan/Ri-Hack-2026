import { useState, type FormEvent } from 'react'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { useAsync } from '@/hooks/use-async'
import { api, ApiError } from '@/lib/api-client'

const MAX_LENGTH = 500 // matches Note.MaxTextLength in App.Domain

const timeFormat = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' })

// Example resource: proves the full round trip React -> API -> EF Core -> PostgreSQL. Delete when no longer useful.
export function NotesExample() {
  const notes = useAsync(api.listNotes)
  const [text, setText] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [submitError, setSubmitError] = useState<{ message: string; invalidInput: boolean }>()

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (submitting) return
    setSubmitting(true)
    setSubmitError(undefined)
    try {
      await api.createNote({ text })
      setText('')
      notes.reload()
    } catch (error) {
      setSubmitError({
        message: error instanceof Error ? error.message : 'Could not save the note.',
        invalidInput: error instanceof ApiError && error.status === 400,
      })
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>
          <h2>Example: notes</h2>
        </CardTitle>
        <CardDescription>
          Writes and reads rows through <code className="font-mono text-xs">/api/notes</code>.
        </CardDescription>
      </CardHeader>
      <CardContent className="grid gap-6">
        <form onSubmit={handleSubmit} className="grid gap-2">
          <label htmlFor="note-text" className="text-sm font-medium">
            New note
          </label>
          <div className="flex gap-2">
            <Input
              id="note-text"
              value={text}
              onChange={(event) => setText(event.target.value)}
              required
              maxLength={MAX_LENGTH}
              placeholder="Anything, it is just a demo"
              aria-invalid={submitError?.invalidInput || undefined}
              aria-describedby={submitError ? 'note-error' : undefined}
            />
            <Button type="submit" aria-disabled={submitting}>
              {submitting ? 'Saving…' : 'Add'}
            </Button>
          </div>
          {submitError && (
            <p id="note-error" role="alert" className="text-sm text-destructive">
              {submitError.message}
            </p>
          )}
        </form>

        <div aria-live="polite" aria-busy={notes.loading}>
          {notes.error ? (
            <div role="alert" className="grid justify-items-start gap-2 text-sm">
              <p className="text-destructive">{notes.error.message}</p>
              <Button variant="outline" size="sm" onClick={notes.reload}>
                Try again
              </Button>
            </div>
          ) : !notes.data ? (
            <p className="text-sm text-muted-foreground">Loading notes…</p>
          ) : notes.data.length === 0 ? (
            <p className="text-sm text-muted-foreground">No notes yet. Add one to write a row to PostgreSQL.</p>
          ) : (
            <ul className="divide-y divide-border">
              {notes.data.map((note) => (
                <li key={note.id} className="flex items-baseline justify-between gap-4 py-2 text-sm">
                  <span className="min-w-0 wrap-anywhere">{note.text}</span>
                  <time dateTime={note.createdAt} className="shrink-0 font-mono text-xs text-muted-foreground">
                    {timeFormat.format(new Date(note.createdAt))}
                  </time>
                </li>
              ))}
            </ul>
          )}
        </div>
      </CardContent>
    </Card>
  )
}
