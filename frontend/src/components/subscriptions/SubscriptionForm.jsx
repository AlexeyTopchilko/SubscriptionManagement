import { useState } from 'react'
import { FormField, Input, Select, Button } from '../shared'
import { PLANS, BILLING_CYCLES } from '../../utils/constants'

const today = new Date().toISOString().split('T')[0]

export function SubscriptionForm({ initial = {}, onSubmit, onCancel, loading }) {
  const [form, setForm] = useState({
    plan: initial.plan || 'Pro',
    price: initial.price ?? 0,
    billingCycle: initial.billingCycle || 'monthly',
    startDate: initial.startDate ? initial.startDate.split('T')[0] : today,
    endDate: initial.endDate ? initial.endDate.split('T')[0] : '',
    notes: initial.notes || '',
  })
  const [errors, setErrors] = useState({})

  const set = (field) => (e) => setForm((f) => ({ ...f, [field]: e.target.value }))

  const validate = () => {
    const errs = {}
    if (!form.plan) errs.plan = 'Select a plan'
    if (form.price < 0) errs.price = 'Price cannot be negative'
    if (!form.startDate) errs.startDate = 'Specify a start date'
    if (form.endDate && form.endDate <= form.startDate) errs.endDate = 'Must be later than start date'
    return errs
  }

  const handleSubmit = async (e) => {
    e.preventDefault()
    const errs = validate()
    if (Object.keys(errs).length) { setErrors(errs); return }
    await onSubmit({
      plan: form.plan,
      price: parseFloat(form.price),
      billingCycle: form.billingCycle,
      startDate: new Date(form.startDate).toISOString(),
      endDate: form.endDate ? new Date(form.endDate).toISOString() : null,
      notes: form.notes.trim() || null,
    })
  }

  return (
    <form onSubmit={handleSubmit}>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0 16px' }}>
        <FormField label="Plan *" error={errors.plan}>
          <Select value={form.plan} onChange={set('plan')} error={errors.plan}>
            {PLANS.map(p => <option key={p} value={p}>{p}</option>)}
          </Select>
        </FormField>
        <FormField label="Billing cycle">
          <Select value={form.billingCycle} onChange={set('billingCycle')}>
            {Object.entries(BILLING_CYCLES).map(([v, l]) => <option key={v} value={v}>{l}</option>)}
          </Select>
        </FormField>
      </div>
      <FormField label="Price ($) *" error={errors.price}>
        <Input type="number" min="0" step="0.01" value={form.price} onChange={set('price')} error={errors.price} />
      </FormField>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0 16px' }}>
        <FormField label="Start date *" error={errors.startDate}>
          <Input type="date" value={form.startDate} onChange={set('startDate')} error={errors.startDate} />
        </FormField>
        <FormField label="End date" error={errors.endDate}>
          <Input type="date" value={form.endDate} onChange={set('endDate')} error={errors.endDate} />
        </FormField>
      </div>
      <FormField label="Notes">
        <textarea
          value={form.notes}
          onChange={set('notes')}
          rows={3}
          placeholder="Additional information..."
          style={{
            width: '100%', padding: '8px 12px', borderRadius: 8, fontSize: 14,
            border: '1px solid #e5e7eb', outline: 'none', fontFamily: 'inherit',
            resize: 'vertical', boxSizing: 'border-box', color: '#111',
          }}
        />
      </FormField>
      <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end', marginTop: 8 }}>
        <Button type="button" variant="secondary" onClick={onCancel}>Cancel</Button>
        <Button type="submit" disabled={loading}>{loading ? 'Saving...' : 'Save'}</Button>
      </div>
    </form>
  )
}
