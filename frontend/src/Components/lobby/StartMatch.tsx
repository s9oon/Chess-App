import { useState } from 'react'
import { useNavigate } from 'react-router'
import { chessHub, errorMessage } from '../../api/hub'
import type { ChessModel } from '../../generated/backend.Chess.Engine'

type Props = {
  onError: (message: string) => void
}

// One entry per bot. Record<ChessModel, ...> means a new model in ChessModel.cs (after regenerating)
// is a compile error here until it gets a label, so the dropdown can't silently miss one.
const MODEL_LABELS: Record<ChessModel, string> = {
  Goataro: 'Goataro',
}
const MODELS = Object.keys(MODEL_LABELS) as ChessModel[]

// Creates a new match and goes to its page. MatchRoom does the JoinMatch.
// Opponent "" = a friend (both seats empty, share the link); a model = that bot already sits in Black.
export function StartMatch({ onError }: Props) {
  const [opponent, setOpponent] = useState<ChessModel | ''>('')
  const [busy, setBusy] = useState(false)
  const navigate = useNavigate()

  async function start() {
    setBusy(true)
    try {
      const matchId = await chessHub.createMatch(opponent || undefined)   // undefined -> null -> no bot
      navigate(`/match/${matchId}`)
    } catch (e) {
      onError(errorMessage(e))
      setBusy(false)
    }
  }

  return (
    <div className="start-match">
      <select
        value={opponent}
        onChange={(e) => setOpponent(e.target.value as ChessModel | '')}
        aria-label="Opponent"
        disabled={busy}
      >
        <option value="">vs a friend</option>
        {MODELS.map((model) => (
          <option key={model} value={model}>vs {MODEL_LABELS[model]} (bot)</option>
        ))}
      </select>
      <button onClick={start} disabled={busy}>
        {busy ? 'Starting…' : 'Start match'}
      </button>
    </div>
  )
}
