import { useState } from 'react'
import { errorMessage, hub } from '../api/hub'

type Props = {
  onJoined: (matchId: string, counter: number) => void
  onError: (message: string) => void
}

// Creates a new match, then joins it (CreateMatch only makes empty seats).
export function StartMatch({ onJoined, onError }: Props) {
  const [busy, setBusy] = useState(false)

  async function start() {
    setBusy(true)
    try {
      const matchId = await hub.invoke<string>('CreateMatch')
      const counter = await hub.invoke<number>('JoinMatch', matchId)
      onJoined(matchId, counter)
    } catch (e) {
      onError(errorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <button onClick={start} disabled={busy}>
      {busy ? 'Starting…' : 'Start match'}
    </button>
  )
}
