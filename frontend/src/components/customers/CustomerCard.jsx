import { Avatar, Button, StatusBadge } from '../shared'
import { SUBSCRIPTION_STATUSES } from '../../utils/constants'

export function CustomerCard({ customer, onEdit, onDelete, onClick }) {
  const activeCount = customer.subscriptions?.filter(s => s.status === 'Active').length ?? 0
  const totalCount = customer.subscriptions?.length ?? 0

  return (
    <div
      onClick={onClick}
      style={{
        background: '#fff',
        border: '1px solid #e5e7eb',
        borderRadius: 12,
        padding: '20px',
        cursor: 'pointer',
        transition: 'box-shadow 0.15s, border-color 0.15s',
      }}
      onMouseEnter={e => { e.currentTarget.style.boxShadow = '0 4px 20px rgba(0,0,0,0.08)'; e.currentTarget.style.borderColor = '#c7d2fe' }}
      onMouseLeave={e => { e.currentTarget.style.boxShadow = 'none'; e.currentTarget.style.borderColor = '#e5e7eb' }}
    >
      <div style={{ display: 'flex', alignItems: 'flex-start', gap: 12, marginBottom: 16 }}>
        <Avatar name={customer.name} size={44} />
        <div style={{ flex: 1, minWidth: 0 }}>
          <h3 style={{ margin: 0, fontSize: 15, fontWeight: 600, color: '#111', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
            {customer.name}
          </h3>
          <p style={{ margin: '2px 0 0', fontSize: 13, color: '#6b7280', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
            {customer.email}
          </p>
          {customer.company && (
            <p style={{ margin: '2px 0 0', fontSize: 12, color: '#9ca3af' }}>{customer.company}</p>
          )}
        </div>
        <div style={{ display: 'flex', gap: 4 }} onClick={e => e.stopPropagation()}>
          <Button size="sm" variant="ghost" onClick={onEdit}>✏</Button>
          <Button size="sm" variant="ghost" onClick={onDelete} style={{ color: '#ef4444' }}>🗑</Button>
        </div>
      </div>

      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
        <span style={{ fontSize: 12, color: '#6b7280', background: '#f9fafb', border: '1px solid #e5e7eb', borderRadius: 6, padding: '3px 8px' }}>
          {totalCount === 0 ? 'No subscriptions' : `${totalCount} sub.`}
        </span>
        {activeCount > 0 && (
          <span style={{ fontSize: 12, color: '#059669', background: '#f0fdf4', border: '1px solid #bbf7d0', borderRadius: 6, padding: '3px 8px' }}>
            {activeCount} active
          </span>
        )}
      </div>
    </div>
  )
}
