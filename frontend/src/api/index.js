const BASE_URL = '/api'

async function request(url, options = {}) {
  const res = await fetch(`${BASE_URL}${url}`, {
    headers: { 'Content-Type': 'application/json', ...options.headers },
    ...options,
  })

  if (!res.ok) {
    const error = await res.json().catch(() => ({ message: `HTTP ${res.status}` }))
    throw new Error(error.message || `HTTP ${res.status}`)
  }

  if (res.status === 204) return null
  return res.json()
}

// Customers
export const customersApi = {
  getAll: () => request('/customers'),
  getById: (id) => request(`/customers/${id}`),
  create: (data) => request('/customers', { method: 'POST', body: JSON.stringify(data) }),
  update: (id, data) => request(`/customers/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  delete: (id) => request(`/customers/${id}`, { method: 'DELETE' }),
}

// Subscriptions
export const subscriptionsApi = {
  getAll: (customerId) => request(`/customers/${customerId}/subscriptions`),
  getById: (customerId, id) => request(`/customers/${customerId}/subscriptions/${id}`),
  create: (customerId, data) =>
    request(`/customers/${customerId}/subscriptions`, { method: 'POST', body: JSON.stringify(data) }),
  update: (customerId, id, data) =>
    request(`/customers/${customerId}/subscriptions/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
  updateStatus: (customerId, id, status) =>
    request(`/customers/${customerId}/subscriptions/${id}/status`, {
      method: 'PATCH',
      body: JSON.stringify({ status }),
    }),
  delete: (customerId, id) =>
    request(`/customers/${customerId}/subscriptions/${id}`, { method: 'DELETE' }),
}
