import { useEffect, useState } from 'react'
import { connect, errorMessage } from './api/hub'
import { HomePage } from './Pages/HomePage'
import { MatchRoom } from './Pages/MatchRoom'

type Connection = 'connecting' | 'connected' | 'failed'
type Match = { id: string; counter: number }

// Connects to the hub once, then shows the home page or the match room.
// "Page" here is just which component is rendered: in a match -> MatchRoom, otherwise HomePage.
function App() {
  const [connection, setConnection] = useState<Connection>('connecting')
  const [error, setError] = useState<string | null>(null)
  const [match, setMatch] = useState<Match | null>(null)

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

  if (match)
    return (
      <MatchRoom
        matchId={match.id}
        initialCounter={match.counter}
        onExit={() => setMatch(null)}
      />
    )

  return <HomePage onJoined={(id, counter) => setMatch({ id, counter })} />
}

export default App
