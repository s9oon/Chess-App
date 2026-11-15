import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router'
import './Styles/index.css'
import App from './App.tsx'

// BrowserRouter reads the URL (e.g. /match/a1b2c3d4) so <Routes> in App can pick the page.
createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <App />
    </BrowserRouter>
  </StrictMode>,
)
