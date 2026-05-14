import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useCustomers } from '../hooks/useCustomers'
import { CustomerCard } from '../components/customers/CustomerCard'
import { CustomerForm } from '../components/customers/CustomerForm'
import { Button, Modal, Spinner, Empty, ErrorMsg } from '../components/shared'

export function CustomersPage() {
  const { customers, loading, error, createCustomer, updateCustomer, deleteCustomer } = useCustomers()
  const navigate = useNavigate()
  const [modalState, setModalState] = useState({ open: false, mode: 'create', customer: null })
  const [formLoading, setFormLoading] = useState(false)
  const [formError, setFormError] = useState(null)
  const [search, setSearch] = useState('')

  const openCreate = () => setModalState({ open: true, mode: 'create', customer: null })
  const openEdit = (c) => setModalState({ open: true, mode: 'edit', customer: c })
  const closeModal = () => { setModalState({ open: false, mode: 'create', customer: null }); setFormError(null) }

  const handleSubmit = async (data) => {
    setFormLoading(true)
    setFormError(null)
    try {
      if (modalState.mode === 'create') await createCustomer(data)
      else await updateCustomer(modalState.customer.id, data)
      closeModal()
    } catch (e) {
      setFormError(e.message)
    } finally {
      setFormLoading(false)
    }
  }

  const handleDelete = async (id, name) => {
    if (!confirm(`Delete customer "${name}"? All of their subscriptions will also be deleted.`)) return
    try { await deleteCustomer(id) }
    catch (e) { alert(e.message) }
  }

  const filtered = customers.filter(c =>
    `${c.name} ${c.email} ${c.company || ''}`.toLowerCase().includes(search.toLowerCase())
  )

  // Stats
  const totalSubs = customers.reduce((n, c) => n + (c.subscriptions?.length || 0), 0)
  const activeSubs = customers.reduce((n, c) => n + (c.subscriptions?.filter(s => s.status === 'Active').length || 0), 0)

  return (
    <div style={{ maxWidth: 1100, margin: '0 auto', padding: '32px 24px' }}>
      {/* Header */}
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 32, flexWrap: 'wrap', gap: 16 }}>
        <div>
          <h1 style={{ margin: 0, fontSize: 28, fontWeight: 700, color: '#0f172a' }}>Customers</h1>
          <p style={{ margin: '4px 0 0', fontSize: 14, color: '#6b7280' }}>Manage customers and subscriptions</p>
        </div>
        <Button onClick={openCreate}>+ Add customer</Button>
      </div>

      {/* Stats */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))', gap: 16, marginBottom: 28 }}>
        {[
          { label: 'Total customers', value: customers.length, icon: '👥' },
          { label: 'Total subscriptions', value: totalSubs, icon: '📋' },
          { label: 'Active subscriptions', value: activeSubs, icon: '✅' },
        ].map(stat => (
          <div key={stat.label} style={{ background: '#fff', border: '1px solid #e5e7eb', borderRadius: 10, padding: '16px 20px' }}>
            <div style={{ fontSize: 22 }}>{stat.icon}</div>
            <div style={{ fontSize: 28, fontWeight: 700, color: '#0f172a', lineHeight: 1.2, marginTop: 6 }}>{stat.value}</div>
            <div style={{ fontSize: 13, color: '#6b7280', marginTop: 2 }}>{stat.label}</div>
          </div>
        ))}
      </div>

      {/* Search */}
      <input
        value={search}
        onChange={e => setSearch(e.target.value)}
        placeholder="Search by name, email, or company..."
        style={{
          width: '100%', padding: '10px 16px', borderRadius: 10, fontSize: 14,
          border: '1px solid #e5e7eb', outline: 'none', marginBottom: 24,
          fontFamily: 'inherit', boxSizing: 'border-box',
        }}
      />

      {error && <ErrorMsg message={error} />}
      {loading ? <Spinner /> : filtered.length === 0 ? (
        <Empty icon="👥" text={search ? 'No customers found' : 'Add your first customer'} />
      ) : (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))', gap: 16 }}>
          {filtered.map(c => (
            <CustomerCard
              key={c.id}
              customer={c}
              onClick={() => navigate(`/customers/${c.id}`)}
              onEdit={(e) => { e?.stopPropagation?.(); openEdit(c) }}
              onDelete={(e) => { e?.stopPropagation?.(); handleDelete(c.id, c.name) }}
            />
          ))}
        </div>
      )}

      <Modal
        isOpen={modalState.open}
        onClose={closeModal}
        title={modalState.mode === 'create' ? 'New customer' : 'Edit customer'}
      >
        {formError && <div style={{ marginBottom: 16 }}><ErrorMsg message={formError} /></div>}
        <CustomerForm
          initial={modalState.customer || {}}
          onSubmit={handleSubmit}
          onCancel={closeModal}
          loading={formLoading}
        />
      </Modal>
    </div>
  )
}
