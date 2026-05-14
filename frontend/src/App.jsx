import { BrowserRouter, Routes, Route, Link, useLocation } from 'react-router-dom'
import { CustomersPage } from './pages/CustomersPage'
import { CustomerDetailPage } from './pages/CustomerDetailPage'

function Layout({ children }) {
  const loc = useLocation()
  return (
    <div style={{ minHeight: '100vh', background: '#f8fafc', fontFamily: "'DM Sans', sans-serif" }}>
      <header style={{
        background: '#0f172a',
        borderBottom: '1px solid #1e293b',
        position: 'sticky', top: 0, zIndex: 40,
      }}>
        <div style={{ maxWidth: 1100, margin: '0 auto', padding: '0 24px', display: 'flex', alignItems: 'center', height: 56, gap: 32 }}>
          <Link to="/" style={{ textDecoration: 'none', display: 'flex', alignItems: 'center', gap: 10 }}>
            <span style={{ fontSize: 20 }}>⚡</span>
            <span style={{ fontWeight: 700, fontSize: 16, color: '#f8fafc', letterSpacing: '-0.3px' }}>SubManager</span>
          </Link>
          <nav style={{ display: 'flex', gap: 4 }}>
            <Link to="/" style={{
              textDecoration: 'none', fontSize: 14, fontWeight: 500,
              color: loc.pathname === '/' ? '#f8fafc' : '#64748b',
              padding: '6px 12px', borderRadius: 6,
              background: loc.pathname === '/' ? '#1e293b' : 'transparent',
            }}>Customers</Link>
          </nav>
        </div>
      </header>
      <main>{children}</main>
    </div>
  )
}

export default function App() {
  return (
    <BrowserRouter>
      <Layout>
        <Routes>
          <Route path="/" element={<CustomersPage />} />
          <Route path="/customers/:id" element={<CustomerDetailPage />} />
        </Routes>
      </Layout>
    </BrowserRouter>
  )
}
