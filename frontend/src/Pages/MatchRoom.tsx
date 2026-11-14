import { useEffect, useState } from 'react'
import { hub } from '../api/hub'
import { Counter } from '../Components/Counter'
import { IncrementButton } from '../Components/IncrementButton'
import { LeaveMatch } from '../Components/LeaveMatch'
import '../Styles/MatchRoom.css'

type Props = {
  matchId: string
  initialCounter: number   // from JoinMatch, so the number is right straight away
  onExit: () => void       // back to the home page (you left, or the server ended the match)
}

// Inside a match: shared counter, +1, leave.
export function MatchRoom({ matchId, initialCounter, onExit }: Props) {
  const [counter, setCounter] = useState(initialCounter)
  const [notice, setNotice] = useState<string | null>(null)

  // Listen for what the server sends while we're in this room.
  useEffect(() => {
    const onCounterUpdated = (value: number) => setCounter(value)
    const onOpponentJoined = () => setNotice('Opponent joined.')
    const onOpponentLeft = () => setNotice('Opponent left.')
    const onMatchEnded = () => onExit()

    hub.on('CounterUpdated', onCounterUpdated)
    hub.on('OpponentJoined', onOpponentJoined)
    hub.on('OpponentLeft', onOpponentLeft)
    hub.on('MatchEnded', onMatchEnded)

    // Cleanup on leaving the page: remove exactly these handlers so they don't stack up.
    return () => {
      hub.off('CounterUpdated', onCounterUpdated)
      hub.off('OpponentJoined', onOpponentJoined)
      hub.off('OpponentLeft', onOpponentLeft)
      hub.off('MatchEnded', onMatchEnded)
    }
  }, [onExit])

  return (
    <main>
      <h1>Match</h1>
      <p>
        Match id <code>{matchId}</code>: share this with your opponent
      </p>

      <Counter value={counter} />

      <div className="actions">
        <IncrementButton matchId={matchId} onError={setNotice} />
        <LeaveMatch matchId={matchId} onLeft={onExit} onError={setNotice} />
      </div>

      {notice && <p className="notice">{notice}</p>}
    </main>
  )
}
