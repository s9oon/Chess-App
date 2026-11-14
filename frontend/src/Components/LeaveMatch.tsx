import { errorMessage, hub } from '../api/hub'

type Props = {
  matchId: string
  onLeft: () => void
  onError: (message: string) => void
}

// Frees your seat. The other player gets "OpponentLeft".
export function LeaveMatch({ matchId, onLeft, onError }: Props) {
  async function leave() {
    try {
      await hub.invoke('LeaveMatch', matchId)
      onLeft()
    } catch (e) {
      onError(errorMessage(e))
    }
  }

  return <button onClick={leave}>Leave match</button>
}
