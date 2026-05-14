import { useState } from 'react'
import { StatusBadge, Button, Select } from '../shared'
import { ALLOWED_TRANSITIONS, SUBSCRIPTION_STATUSES, formatDate, formatPrice } from '../../utils/constants'

export function SubscriptionCard({ sub, onEdit, onDelete, onStatusChange }) {
  const [changingStatus, setChangingStatus] = useState(false)
  const [newStatus, setNewStatus] = useState('')
  const [loading, setLoading] = useState(false)

  const transitions = ALLOWED_TRANSITIONS[sub.status] || []

  const handleStatusApply = async () => {
    if (!newStatus) return
    setLoading(true)
    try { await onStatusChange(sub.id, newStatus) }
    finally { setLoading(false); setChangingStatus(false); setNewStatus('') }
  }

  return (
    <div style={{
      background: '#fff', border: '1px solid #e5e7eb', borderRadius: 10,
      padding: '16px 20px',
    }}>
      <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: 12, flexWrap: 'wrap' }}>
        <div style={{ flex: 1 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, flexWrap: 'wrap', marginBottom: 6 }}>
            <span style={{ fontWeight: 600, fontSize: 15, color: '#111' }}>{sub.plan}</span>
            <StatusBadge status={sub.status} />
          </div>
          <div style={{ fontSize: 13, color: '#374151', fontWeight: 500 }}>
            {formatPrice(sub.price, sub.billingCycle)}
          </div>
          <div style={{ fontSize: 12, color: '#6b7280', marginTop: 4 }}>
            {formatDate(sub.startDate)} — {formatDate(sub.endDate)}
          </div>
          {sub.notes && (
            <div style={{ fontSize: 12, color: '#9ca3af', marginTop: 4, fontStyle: 'italic' }}>{sub.notes}</div>
          )}
        </div>
        <div style={{ display: 'flex', gap: 4 }}>
          {transitions.length > 0 && !changingStatus && (
            <Button size="sm" variant="secondary" onClick={() => setChangingStatus(true)}>Status</Button>
          )}
          <Button size="sm" variant="ghost" onClick={onEdit}>✏</Button>
          <Button size="sm" variant="ghost" onClick={onDelete} style={{ color: '#ef4444' }}>🗑</Button>
        </div>
      </div>

      {changingStatus && (
        <div style={{ display: 'flex', gap: 8, alignItems: 'center', marginTop: 12, paddingTop: 12, borderTop: '1px solid #f3f4f6' }}>
          <Select value={newStatus} onChange={e => setNewStatus(e.target.value)} style={{ flex: 1, fontSize: 13 }}>
            <option value="">Select status...</option>
            {transitions.map(s => (
              <option key={s} value={s}>{SUBSCRIPTION_STATUSES[s]?.label}</option>
            ))}
          </Select>
          <Button size="sm" onClick={handleStatusApply} disabled={!newStatus || loading}>
            {loading ? '...' : 'Apply'}
          </Button>
          <Button size="sm" variant="ghost" onClick={() => { setChangingStatus(false); setNewStatus('') }}>×</Button>
        </div>
      )}
    </div>
  )
}
