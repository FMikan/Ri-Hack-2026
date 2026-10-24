import type { ChangeEvent, ReactNode, SubmitEvent } from 'react'
import { Link } from 'react-router'
import { Button } from '@/components/ui/button'
import { AuthLayout } from './AuthLayout'

interface AuthFormProps {
  copy: {
    documentTitle: string
    title: string
    subtitle: string
    submit: string
    submitting: string
    switchPrompt: string
    switchLink: string
  }
  switchTo: string
  formError?: string
  submitting: boolean
  onSubmit: (event: SubmitEvent<HTMLFormElement>) => void
  onChange: (event: ChangeEvent<HTMLFormElement>) => void
  children: ReactNode
}

// Heading, form error, fields, submit button and the link to the other auth page.
export function AuthForm({ copy, switchTo, formError, submitting, onSubmit, onChange, children }: AuthFormProps) {
  return (
    <AuthLayout title={copy.documentTitle}>
      <h1 className="font-heading text-2xl font-semibold tracking-tight text-balance sm:text-3xl">{copy.title}</h1>
      <p className="mt-2 text-pretty text-muted-foreground short:hidden">{copy.subtitle}</p>

      <form
        noValidate
        onSubmit={onSubmit}
        onChange={onChange}
        className="mt-6 grid gap-4 sm:mt-8 sm:gap-5 short:mt-4 short:gap-3"
      >
        {formError && (
          <p role="alert" className="rounded-lg bg-destructive/10 px-3 py-2.5 text-sm text-destructive">
            {formError}
          </p>
        )}
        {children}
        <Button type="submit" size="lg" className="mt-1 w-full" aria-disabled={submitting}>
          {submitting ? copy.submitting : copy.submit}
        </Button>
      </form>

      <p className="mt-5 text-sm text-muted-foreground short:mt-4">
        {copy.switchPrompt}{' '}
        <Link
          to={switchTo}
          className="rounded-sm font-medium text-foreground underline underline-offset-4 transition-colors hover:text-muted-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
        >
          {copy.switchLink}
        </Link>
      </p>
    </AuthLayout>
  )
}
