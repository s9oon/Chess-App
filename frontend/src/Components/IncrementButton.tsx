import { errorMessage, hub } from '../api/hub'

type Props = {
  matchId: string
  onError: (message: string) => void
}

// Asks the server to +1. The new value comes back to BOTH players through "CounterUpdated",
// so this component doesn't set the counter itself.
export function IncrementButton({ matchId, onError }: Props) {
  async function increment() {
    try {
      await hub.invoke('IncrementCounter', matchId)
    } catch (e) {
      onError(errorMessage(e))
    }
  }

  return <button onClick={increment}>+1</button>
}
