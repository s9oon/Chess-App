import { useEffect, useState } from 'react'
import { getHealthPostgres, getHealthRedis } from './api/client'

type Status = 'checking' | 'up' | 'down'

function App() {
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
      <h1>Backend health</h1>
      <p>API: {import.meta.env.VITE_API_URL ?? '(same origin)'}</p>
      <ul>
        <li>Postgres: <span className={postgres}>{postgres}</span></li>
        <li>Redis: <span className={redis}>{redis}</span></li>
      </ul>
      <button onClick={check}>Check again</button>
    </main>
  )
}

export default App
