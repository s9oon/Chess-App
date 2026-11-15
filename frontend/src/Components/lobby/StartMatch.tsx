import { useState } from 'react'
import { useNavigate } from 'react-router'
import { chessHub, errorMessage } from '../../api/hub'

type Props = {
  onError: (message: string) => void
}

// Creates a new match and goes to its page. MatchRoom does the JoinMatch (CreateMatch only makes empty seats).
export function StartMatch({ onError }: Props) {
  const [busy, setBusy] = useState(false)
  const navigate = useNavigate()

  async function start() {
    setBusy(true)
    try {
      const matchId = await chessHub.createMatch()
      navigate(`/match/${matchId}`)
    } catch (e) {
      onError(errorMessage(e))
      setBusy(false)
    }
  }

  return (
    <button onClick={start} disabled={busy}>
      {busy ? 'Starting…' : 'Start match'}
    </button>
  )
}
