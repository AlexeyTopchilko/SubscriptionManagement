import { SUBSCRIPTION_STATUSES } from '../../utils/constants'

export function StatusBadge({ status }) {
  const cfg = SUBSCRIPTION_STATUSES[status] || {}
  return (
    <span style={{
      display: 'inline-flex', alignItems: 'center', gap: 6,
      padding: '3px 10px', borderRadius: 20,
      fontSize: 12, fontWeight: 500,
      color: cfg.color, background: cfg.bg,
      border: `1px solid ${cfg.border}`,
    }}>
      <span style={{ width: 6, height: 6, borderRadius: '50%', background: cfg.color, flexShrink: 0 }} />
      {cfg.label}
    </span>
  )
}

export function Button({ children, variant = 'primary', size = 'md', disabled, onClick, type = 'button', style }) {
  const base = {
    display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: 6,
    fontFamily: 'inherit', fontWeight: 500, cursor: disabled ? 'not-allowed' : 'pointer',
    border: 'none', borderRadius: 8, transition: 'all 0.15s',
    opacity: disabled ? 0.55 : 1,
  }
  const sizes = { sm: { padding: '5px 12px', fontSize: 13 }, md: { padding: '8px 16px', fontSize: 14 }, lg: { padding: '10px 20px', fontSize: 15 } }
  const variants = {
    primary: { background: '#1a1a2e', color: '#fff' },
    secondary: { background: '#f1f5f9', color: '#374151' },
    danger: { background: '#fef2f2', color: '#dc2626', border: '1px solid #fecaca' },
    ghost: { background: 'transparent', color: '#6b7280' },
  }
  return (
    <button type={type} onClick={onClick} disabled={disabled} style={{ ...base, ...sizes[size], ...variants[variant], ...style }}>
      {children}
    </button>
  )
}

export function Modal({ isOpen, onClose, title, children, width = 520 }) {
  if (!isOpen) return null
  return (
    <div style={{
      position: 'fixed', inset: 0, zIndex: 50,
      background: 'rgba(0,0,0,0.4)', backdropFilter: 'blur(3px)',
      display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 20,
    }} onClick={onClose}>
      <div style={{
        background: '#fff', borderRadius: 16, width: '100%', maxWidth: width,
        maxHeight: '90vh', overflow: 'auto',
        boxShadow: '0 20px 60px rgba(0,0,0,0.15)',
      }} onClick={e => e.stopPropagation()}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '20px 24px 0' }}>
          <h2 style={{ margin: 0, fontSize: 18, fontWeight: 600, color: '#111' }}>{title}</h2>
          <button onClick={onClose} style={{ background: 'none', border: 'none', cursor: 'pointer', fontSize: 20, color: '#9ca3af', lineHeight: 1 }}>×</button>
        </div>
        <div style={{ padding: '20px 24px 24px' }}>{children}</div>
      </div>
    </div>
  )
}

export function FormField({ label, error, children }) {
  return (
    <div style={{ marginBottom: 16 }}>
      {label && <label style={{ display: 'block', fontSize: 13, fontWeight: 500, color: '#374151', marginBottom: 6 }}>{label}</label>}
      {children}
      {error && <p style={{ margin: '4px 0 0', fontSize: 12, color: '#dc2626' }}>{error}</p>}
    </div>
  )
}

export function Input({ error, ...props }) {
  return (
    <input {...props} style={{
      width: '100%', padding: '8px 12px', borderRadius: 8, fontSize: 14,
      border: `1px solid ${error ? '#fca5a5' : '#e5e7eb'}`,
      outline: 'none', fontFamily: 'inherit', boxSizing: 'border-box',
      background: '#fff', color: '#111',
      ...props.style,
    }} />
  )
}

export function Select({ children, error, ...props }) {
  return (
    <select {...props} style={{
      width: '100%', padding: '8px 12px', borderRadius: 8, fontSize: 14,
      border: `1px solid ${error ? '#fca5a5' : '#e5e7eb'}`,
      outline: 'none', fontFamily: 'inherit', background: '#fff', color: '#111',
      boxSizing: 'border-box', cursor: 'pointer',
      ...props.style,
    }}>
      {children}
    </select>
  )
}

export function Spinner() {
  return (
    <div style={{ display: 'flex', justifyContent: 'center', padding: 40 }}>
      <div style={{
        width: 32, height: 32, borderRadius: '50%',
        border: '3px solid #e5e7eb', borderTopColor: '#1a1a2e',
        animation: 'spin 0.7s linear infinite',
      }} />
      <style>{`@keyframes spin { to { transform: rotate(360deg) } }`}</style>
    </div>
  )
}

export function Empty({ icon = '📭', text }) {
  return (
    <div style={{ textAlign: 'center', padding: '48px 24px', color: '#9ca3af' }}>
      <div style={{ fontSize: 36, marginBottom: 12 }}>{icon}</div>
      <p style={{ margin: 0, fontSize: 14 }}>{text}</p>
    </div>
  )
}

export function ErrorMsg({ message }) {
  return (
    <div style={{ background: '#fef2f2', border: '1px solid #fecaca', borderRadius: 8, padding: '12px 16px', color: '#dc2626', fontSize: 14 }}>
      ⚠ {message}
    </div>
  )
}

export function getInitials(name = '') {
  return name.split(' ').slice(0, 2).map(w => w[0]).join('').toUpperCase()
}

const AVATAR_COLORS = ['#4f46e5', '#0891b2', '#059669', '#d97706', '#dc2626', '#7c3aed']
export function Avatar({ name, size = 36 }) {
  const color = AVATAR_COLORS[name.charCodeAt(0) % AVATAR_COLORS.length]
  return (
    <div style={{
      width: size, height: size, borderRadius: '50%',
      background: color, color: '#fff',
      display: 'flex', alignItems: 'center', justifyContent: 'center',
      fontSize: size * 0.38, fontWeight: 600, flexShrink: 0,
    }}>
      {getInitials(name)}
    </div>
  )
}
