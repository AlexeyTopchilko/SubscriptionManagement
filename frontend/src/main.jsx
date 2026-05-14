import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App.jsx'

// Global reset
const style = document.createElement('style')
style.textContent = `
  *, *::before, *::after { box-sizing: border-box; }
  body { margin: 0; padding: 0; -webkit-font-smoothing: antialiased; }
  input, select, textarea, button { font-family: inherit; }
`
document.head.appendChild(style)

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <App />
  </StrictMode>
)
