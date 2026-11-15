import { useEffect, useState } from 'react'
import { Navigate, Route, Routes } from 'react-router'
import { connect, errorMessage } from './api/hub'
import { HomePage } from './Pages/HomePage'
import { MatchRoom } from './Pages/MatchRoom'

type Connection = 'connecting' | 'connected' | 'failed'

// Connects to the hub once, then the URL decides the page:
//   /                 -> HomePage (start or join a match)
//   /match/a1b2c3d4   -> MatchRoom for match a1b2c3d4 (the URL is the invite link)
//   anything else     -> back to /
function App() {
  const [connection, setConnection] = useState<Connection>('connecting')
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    connect()
      .then(() => setConnection('connected'))
      .catch((e) => {
        setConnection('failed')
        setError(errorMessage(e))
      })
  }, [])

  if (connection === 'connecting')
    return <main><p className="checking">Connecting…</p></main>

  if (connection === 'failed')
    return <main><p className="down">Couldn't connect to the hub: {error}</p></main>

  return (
    <Routes>
      <Route path="/" element={<HomePage />} />
      <Route path="/match/:matchId" element={<MatchRoom />} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

export default App
