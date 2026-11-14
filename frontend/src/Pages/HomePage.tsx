import { useEffect, useState } from 'react'
import { getHealthPostgres, getHealthRedis } from '../api/client'
import { StartMatch } from '../Components/StartMatch'
import { JoinMatch } from '../Components/JoinMatch'
import '../Styles/HomePage.css'

type Status = 'checking' | 'up' | 'down'

type Props = {
  onJoined: (matchId: string, counter: number) => void
}

// Lobby: start a new match or join one by id. Also shows backend health.
export function HomePage({ onJoined }: Props) {
  const [error, setError] = useState<string | null>(null)
  const [postgres, setPostgres] = useState<Status>('checking')
  const [redis, setRedis] = useState<Status>('checking')

  async function check() {
    setPostgres('checking')
    setRedis('checking')

    // fetch only throws when the backend can't be reached at all; a 503 still resolves
    const [pg, rd] = await Promise.all([
      getHealthPostgres().catch(() => null),
      getHealthRedis().catch(() => null),
    ])

    setPostgres(pg?.status === 200 ? 'up' : 'down')
    setRedis(rd?.status === 200 ? 'up' : 'down')
  }

  useEffect(() => {
    check()
  }, [])

  return (
    <main>
      <h1>Counter</h1>

      <section className="lobby">
        <StartMatch onJoined={onJoined} onError={setError} />
        <span>or</span>
        <JoinMatch onJoined={onJoined} onError={setError} />
      </section>

      {error && <p className="down">{error}</p>}

      <h2>Backend health</h2>
      <p>API: {import.meta.env.VITE_API_URL ?? '(same origin)'}</p>
      <ul>
        <li>Postgres: <span className={postgres}>{postgres}</span></li>
        <li>Redis: <span className={redis}>{redis}</span></li>
      </ul>
      <button onClick={check}>Check again</button>
    </main>
  )
}
