import { useState, type FormEvent } from 'react'
import { useNavigate } from 'react-router'

// Goes to /match/{id} for a typed-in id. Same as opening the invite link; MatchRoom does the JoinMatch.
export function JoinMatch() {
  const [matchId, setMatchId] = useState('')
  const navigate = useNavigate()

  function join(e: FormEvent) {
    e.preventDefault()
    const id = matchId.trim()
    if (id) navigate(`/match/${id}`)
  }

  return (
    <form onSubmit={join}>
      <input
        value={matchId}
        onChange={(e) => setMatchId(e.target.value)}
        placeholder="Match id"
        aria-label="Match id"
      />
      <button type="submit" disabled={!matchId.trim()}>Join match</button>
    </form>
  )
}
