import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router'
import { chessHub, errorMessage, hub } from '../api/hub'
import { getReceiverRegister } from '../generated/TypedSignalR.Client'
import type { GameState } from '../generated/backend.DTO'
import { LeaveMatch } from '../Components/LeaveMatch'
import '../Styles/MatchRoom.css'

// /match/:matchId. Joins the match from the URL (so a refresh or an invite link both work).
// Bare on purpose: the board UI comes next.
export function MatchRoom() {
  const { matchId } = useParams() as { matchId: string }   // the route only matches when it's there
  const navigate = useNavigate()
  const [game, setGame] = useState<GameState | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  // Join on open. Rejoining your own seat is fine (ChessService.Join handles it),
  // so StrictMode running this twice in dev is harmless.
  useEffect(() => {
    chessHub.joinMatch(matchId)
      .then(setGame)
      .catch((e) => setNotice(errorMessage(e)))
  }, [matchId])

  // What the server sends while we're in this room. Must match IChessClient exactly.
  useEffect(() => {
    const sub = getReceiverRegister('IChessClient').register(hub, {
      gameUpdated: async (state) => setGame(state),
      opponentJoined: async () => setNotice('Opponent joined.'),
      opponentLeft: async () => setNotice('Opponent left.'),
      matchEnded: async () => navigate('/'),
    })
    return () => sub.dispose()
  }, [navigate])

  return (
    <main>
      <h1>Match</h1>
      <p>
        Match id <code>{matchId}</code>: share this page's link with your opponent
      </p>

      {/* TODO: the board. For now, just the raw state. */}
      {game && <pre>{JSON.stringify(game, null, 2)}</pre>}

      <div className="actions">
        <LeaveMatch matchId={matchId} onLeft={() => navigate('/')} onError={setNotice} />
      </div>

      {notice && <p className="notice">{notice}</p>}
    </main>
  )
}
