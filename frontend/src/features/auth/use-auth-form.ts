import { useState, type ChangeEvent, type SubmitEvent } from 'react'
import { useLocale } from '@/lib/i18n/locale-context'
import { toAuthErrors, type AuthField, type FieldErrors } from './validation'

// Submit flow shared by login and register: validate, call the API, show errors and focus the first bad field.
// On success the route guard navigates away, so there is nothing to do here.
export function useAuthForm<F extends AuthField>(
  fields: F[],
  validate: (values: Record<F, string>) => FieldErrors,
  submit: (values: Record<F, string>) => Promise<void>,
) {
  const { t } = useLocale()
  const [errors, setErrors] = useState<FieldErrors>({})
  const [formError, setFormError] = useState<string>()
  const [submitting, setSubmitting] = useState(false)

  function show(form: HTMLFormElement, fieldErrors: FieldErrors, message?: string) {
    setErrors(fieldErrors)
    setFormError(message)
    const first = fields.find((field) => fieldErrors[field])
    if (first) (form.elements.namedItem(first) as HTMLInputElement | null)?.focus()
  }

  async function onSubmit(event: SubmitEvent<HTMLFormElement>) {
    event.preventDefault()
    if (submitting) return
    const form = event.currentTarget
    const data = new FormData(form)
    // The server trims e-mail and username as well; passwords are sent exactly as typed.
    const values = Object.fromEntries(
      fields.map((field) => {
        const value = String(data.get(field) ?? '')
        return [field, field === 'password' ? value : value.trim()]
      }),
    ) as Record<F, string>

    const invalid = validate(values)
    if (fields.some((field) => invalid[field])) return show(form, invalid)

    show(form, {})
    setSubmitting(true)
    try {
      await submit(values)
    } catch (error) {
      const result = toAuthErrors(error, t)
      show(form, result.fields, result.form)
    } finally {
      setSubmitting(false)
    }
  }

  // Typing in a field clears its error; the form re-checks everything on the next submit.
  function onChange(event: ChangeEvent<HTMLFormElement>) {
    const name = event.target.getAttribute('name') as AuthField
    if (errors[name]) setErrors((previous) => ({ ...previous, [name]: undefined }))
  }

  return { errors, formError, submitting, onSubmit, onChange }
}
