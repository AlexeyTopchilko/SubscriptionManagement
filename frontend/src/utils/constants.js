export const SUBSCRIPTION_STATUSES = {
  Active: { label: 'Active', color: '#22c55e', bg: '#f0fdf4', border: '#bbf7d0' },
  Paused: { label: 'Paused', color: '#f59e0b', bg: '#fffbeb', border: '#fde68a' },
  Cancelled: { label: 'Cancelled', color: '#ef4444', bg: '#fef2f2', border: '#fecaca' },
  Future: { label: 'Scheduled', color: '#6366f1', bg: '#eef2ff', border: '#c7d2fe' },
}

export const BILLING_CYCLES = {
  monthly: 'Monthly',
  annual: 'Annual',
}

export const PLANS = ['Starter', 'Basic', 'Pro', 'Enterprise']

// Status transitions allowed
export const ALLOWED_TRANSITIONS = {
  Future: ['Active', 'Cancelled'],
  Active: ['Paused', 'Cancelled'],
  Paused: ['Active', 'Cancelled'],
  Cancelled: [],
}

export function formatDate(dateStr) {
  if (!dateStr) return '—'
  return new Date(dateStr).toLocaleDateString('en-US', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
  })
}

export function formatPrice(price, billingCycle) {
  const formatted = new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 0,
  }).format(price)
  return `${formatted} / ${billingCycle === 'annual' ? 'yr' : 'mo'}`
}

export function getInitials(name) {
  return name
    .split(' ')
    .slice(0, 2)
    .map((w) => w[0])
    .join('')
    .toUpperCase()
}
