import clsx from 'clsx'
import type { InputHTMLAttributes, SelectHTMLAttributes, TextareaHTMLAttributes } from 'react'
import { forwardRef } from 'react'

interface BaseProps {
  label?: string
  error?: string
  hint?: string
}

export const Input = forwardRef<HTMLInputElement, BaseProps & InputHTMLAttributes<HTMLInputElement>>(
  ({ label, error, hint, className, ...rest }, ref) => (
    <div>
      {label && <label className="label">{label}</label>}
      <input ref={ref} {...rest} className={clsx('input', error && 'border-rose-400', className)} />
      {error ? <p className="field-error">{error}</p>
        : hint && <p className="mt-1 text-xs text-slate-400">{hint}</p>}
    </div>
  ),
)
Input.displayName = 'Input'

export const Textarea = forwardRef<
  HTMLTextAreaElement,
  BaseProps & TextareaHTMLAttributes<HTMLTextAreaElement>
>(({ label, error, hint, className, ...rest }, ref) => (
  <div>
    {label && <label className="label">{label}</label>}
    <textarea ref={ref} {...rest} className={clsx('input', error && 'border-rose-400', className)} />
    {error ? <p className="field-error">{error}</p>
      : hint && <p className="mt-1 text-xs text-slate-400">{hint}</p>}
  </div>
))
Textarea.displayName = 'Textarea'

export const Select = forwardRef<
  HTMLSelectElement,
  BaseProps & SelectHTMLAttributes<HTMLSelectElement>
>(({ label, error, hint, className, children, ...rest }, ref) => (
  <div>
    {label && <label className="label">{label}</label>}
    <select ref={ref} {...rest} className={clsx('input', error && 'border-rose-400', className)}>
      {children}
    </select>
    {error ? <p className="field-error">{error}</p>
      : hint && <p className="mt-1 text-xs text-slate-400">{hint}</p>}
  </div>
))
Select.displayName = 'Select'
