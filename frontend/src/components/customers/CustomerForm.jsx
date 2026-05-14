import { useState } from 'react'
import { FormField, Input, Button } from '../shared'

export function CustomerForm({ initial = {}, onSubmit, onCancel, loading }) {
  const [form, setForm] = useState({
    name: initial.name || '',
    email: initial.email || '',
    company: initial.company || '',
    phone: initial.phone || '',
  })
  const [errors, setErrors] = useState({})

  const set = (field) => (e) => setForm((f) => ({ ...f, [field]: e.target.value }))

  const validate = () => {
    const errs = {}
    if (!form.name.trim()) errs.name = 'Enter name'
    if (!form.email.trim()) errs.email = 'Enter email'
    else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email)) errs.email = 'Invalid email'
    return errs
  }

  const handleSubmit = async (e) => {
    e.preventDefault()
    const errs = validate()
    if (Object.keys(errs).length) { setErrors(errs); return }
    await onSubmit({
      name: form.name.trim(),
      email: form.email.trim(),
      company: form.company.trim() || null,
      phone: form.phone.trim() || null,
    })
  }

  return (
    <form onSubmit={handleSubmit}>
      <FormField label="Name *" error={errors.name}>
        <Input value={form.name} onChange={set('name')} placeholder="John Doe" error={errors.name} />
      </FormField>
      <FormField label="Email *" error={errors.email}>
        <Input type="email" value={form.email} onChange={set('email')} placeholder="john@example.com" error={errors.email} />
      </FormField>
      <FormField label="Company">
        <Input value={form.company} onChange={set('company')} placeholder="Acme Inc." />
      </FormField>
      <FormField label="Phone">
        <Input value={form.phone} onChange={set('phone')} placeholder="+1 (555) 000-0000" />
      </FormField>
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
        <Button type="button" variant="secondary" onClick={onCancel}>Cancel</Button>
        <Button type="submit" disabled={loading}>{loading ? 'Saving...' : 'Save'}</Button>
      </div>
    </form>
  )
}
