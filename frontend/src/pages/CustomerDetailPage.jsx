import { useState, useEffect } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { customersApi } from '../api'
import { useSubscriptions } from '../hooks/useSubscriptions'
import { SubscriptionCard } from '../components/subscriptions/SubscriptionCard'
import { SubscriptionForm } from '../components/subscriptions/SubscriptionForm'
import { Avatar, Button, Modal, Spinner, Empty, ErrorMsg, StatusBadge } from '../components/shared'
import { formatDate } from '../utils/constants'

export function CustomerDetailPage() {
  const { id } = useParams()
  const navigate = useNavigate()
  const [customer, setCustomer] = useState(null)
  const [custLoading, setCustLoading] = useState(true)
  const [custError, setCustError] = useState(null)

  const {
    subscriptions, loading: subsLoading, error: subsError,
    createSubscription, updateSubscription, updateStatus, deleteSubscription
  } = useSubscriptions(id)

  const [modalState, setModalState] = useState({ open: false, mode: 'create', sub: null })
  const [formLoading, setFormLoading] = useState(false)
  const [formError, setFormError] = useState(null)

  useEffect(() => {
    customersApi.getById(id)
      .then(setCustomer)
      .catch(e => setCustError(e.message))
      .finally(() => setCustLoading(false))
  }, [id])

  const openCreate = () => setModalState({ open: true, mode: 'create', sub: null })
  const openEdit = (sub) => setModalState({ open: true, mode: 'edit', sub })
  const closeModal = () => { setModalState({ open: false, mode: 'create', sub: null }); setFormError(null) }

  const handleSubmit = async (data) => {
    setFormLoading(true); setFormError(null)
    try {
      if (modalState.mode === 'create') await createSubscription(data)
      else await updateSubscription(modalState.sub.id, data)
      closeModal()
    } catch (e) { setFormError(e.message) }
    finally { setFormLoading(false) }
  }

  const handleDelete = async (subId) => {
    if (!confirm('Delete subscription?')) return
    try { await deleteSubscription(subId) }
    catch (e) { alert(e.message) }
  }

  const handleStatusChange = async (subId, status) => {
    try { await updateStatus(subId, status) }
    catch (e) { alert(e.message) }
  }

  if (custLoading) return <div style={{ padding: 40 }}><Spinner /></div>
  if (custError) return <div style={{ padding: 40 }}><ErrorMsg message={custError} /></div>
  if (!customer) return null

  const activeCount = subscriptions.filter(s => s.status === 'Active').length

  return (
    <div style={{ maxWidth: 860, margin: '0 auto', padding: '32px 24px' }}>
      {/* Back */}
      <button onClick={() => navigate('/')} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#6b7280', fontSize: 14, marginBottom: 24, padding: 0, display: 'flex', alignItems: 'center', gap: 6 }}>
        ← All customers
      </button>

      {/* Customer header */}
      <div style={{ background: '#fff', border: '1px solid #e5e7eb', borderRadius: 12, padding: '24px', marginBottom: 24 }}>
        <div style={{ display: 'flex', alignItems: 'flex-start', gap: 16, flexWrap: 'wrap' }}>
          <Avatar name={customer.name} size={56} />
          <div style={{ flex: 1 }}>
            <h1 style={{ margin: 0, fontSize: 22, fontWeight: 700, color: '#0f172a' }}>{customer.name}</h1>
            <p style={{ margin: '4px 0 0', fontSize: 14, color: '#6b7280' }}>{customer.email}</p>
            {customer.company && <p style={{ margin: '2px 0 0', fontSize: 13, color: '#9ca3af' }}>{customer.company}</p>}
            {customer.phone && <p style={{ margin: '2px 0 0', fontSize: 13, color: '#9ca3af' }}>{customer.phone}</p>}
          </div>
          <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
            <div style={{ textAlign: 'right' }}>
              <div style={{ fontSize: 24, fontWeight: 700, color: '#0f172a' }}>{subscriptions.length}</div>
              <div style={{ fontSize: 12, color: '#6b7280' }}>subscriptions</div>
            </div>
            <div style={{ textAlign: 'right' }}>
              <div style={{ fontSize: 24, fontWeight: 700, color: '#059669' }}>{activeCount}</div>
              <div style={{ fontSize: 12, color: '#6b7280' }}>active</div>
            </div>
          </div>
        </div>
        <div style={{ fontSize: 12, color: '#9ca3af', marginTop: 12 }}>
          Customer since {formatDate(customer.createdAt)}
        </div>
      </div>

      {/* Subscriptions */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 16 }}>
        <h2 style={{ margin: 0, fontSize: 18, fontWeight: 600, color: '#0f172a' }}>Subscriptions</h2>
        <Button onClick={openCreate}>+ Add subscription</Button>
      </div>

      {subsError && <ErrorMsg message={subsError} />}
      {subsLoading ? <Spinner /> : subscriptions.length === 0 ? (
        <Empty icon="📋" text="This customer has no subscriptions yet" />
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          {subscriptions.map(sub => (
            <SubscriptionCard
              key={sub.id}
              sub={sub}
              onEdit={() => openEdit(sub)}
              onDelete={() => handleDelete(sub.id)}
              onStatusChange={handleStatusChange}
            />
          ))}
        </div>
      )}

      <Modal
        isOpen={modalState.open}
        onClose={closeModal}
        title={modalState.mode === 'create' ? 'New subscription' : 'Edit subscription'}
      >
        {formError && <div style={{ marginBottom: 16 }}><ErrorMsg message={formError} /></div>}
        <SubscriptionForm
          initial={modalState.sub || {}}
          onSubmit={handleSubmit}
          onCancel={closeModal}
          loading={formLoading}
        />
      </Modal>
    </div>
  )
}
