import { useState, useEffect, useCallback } from 'react'
import { subscriptionsApi } from '../api'

export function useSubscriptions(customerId) {
  const [subscriptions, setSubscriptions] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  const fetchSubscriptions = useCallback(async () => {
    if (!customerId) return
    try {
      setLoading(true)
      setError(null)
      const data = await subscriptionsApi.getAll(customerId)
      setSubscriptions(data)
    } catch (err) {
      setError(err.message)
    } finally {
      setLoading(false)
    }
  }, [customerId])

  useEffect(() => { fetchSubscriptions() }, [fetchSubscriptions])

  const createSubscription = async (data) => {
    const created = await subscriptionsApi.create(customerId, data)
    setSubscriptions((prev) => [created, ...prev])
    return created
  }

  const updateSubscription = async (id, data) => {
    const updated = await subscriptionsApi.update(customerId, id, data)
    setSubscriptions((prev) => prev.map((s) => (s.id === id ? updated : s)))
    return updated
  }

  const updateStatus = async (id, status) => {
    const updated = await subscriptionsApi.updateStatus(customerId, id, status)
    setSubscriptions((prev) => prev.map((s) => (s.id === id ? updated : s)))
    return updated
  }

  const deleteSubscription = async (id) => {
    await subscriptionsApi.delete(customerId, id)
    setSubscriptions((prev) => prev.filter((s) => s.id !== id))
  }

  return {
    subscriptions, loading, error,
    refetch: fetchSubscriptions,
    createSubscription, updateSubscription, updateStatus, deleteSubscription,
  }
}
