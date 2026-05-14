import { useState, useEffect, useCallback } from 'react'
import { customersApi } from '../api'

export function useCustomers() {
  const [customers, setCustomers] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  const fetchCustomers = useCallback(async () => {
    try {
      setLoading(true)
      setError(null)
      const data = await customersApi.getAll()
      setCustomers(data)
    } catch (err) {
      setError(err.message)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => { fetchCustomers() }, [fetchCustomers])

  const createCustomer = async (data) => {
    const created = await customersApi.create(data)
    setCustomers((prev) => [...prev, created])
    return created
  }

  const updateCustomer = async (id, data) => {
    const updated = await customersApi.update(id, data)
    setCustomers((prev) => prev.map((c) => (c.id === id ? updated : c)))
    return updated
  }

  const deleteCustomer = async (id) => {
    await customersApi.delete(id)
    setCustomers((prev) => prev.filter((c) => c.id !== id))
  }

  return { customers, loading, error, refetch: fetchCustomers, createCustomer, updateCustomer, deleteCustomer }
}
