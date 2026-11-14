import { useState, type FormEvent } from 'react'
import { errorMessage, hub } from '../api/hub'

type Props = {
  onJoined: (matchId: string, counter: number) => void
  onError: (message: string) => void
}

// Joins an existing match by its id (the one the other player shares).
export function JoinMatch({ onJoined, onError }: Props) {
  const [matchId, setMatchId] = useState('')
  const [busy, setBusy] = useState(false)

  async function join(e: FormEvent) {
    e.preventDefault()
    const id = matchId.trim()
    if (!id) return

    setBusy(true)
    try {
      const counter = await hub.invoke<number>('JoinMatch', id)
      onJoined(id, counter)
    } catch (e) {
      onError(errorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={join}>
      <input
        value={matchId}
        onChange={(e) => setMatchId(e.target.value)}
        placeholder="Match id"
        aria-label="Match id"
      />
      <button type="submit" disabled={busy || !matchId.trim()}>
        {busy ? 'Joining…' : 'Join match'}
      </button>
    </form>
  )
}
