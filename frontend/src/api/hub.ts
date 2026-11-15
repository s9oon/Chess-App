import * as signalR from '@microsoft/signalr'
import { postSession } from './client'
import { getHubProxyFactory } from '../generated/TypedSignalR.Client'

// Same base URL as fetcher.ts. Unset (local dev) = relative URLs, handled by the Vite proxy.
const baseUrl = import.meta.env.VITE_API_URL ?? ''

// One connection for the whole app. Created here (not inside a component) so React
// re-rendering or StrictMode mounting twice can't open a second connection.
export const hub = new signalR.HubConnectionBuilder()
  .withUrl(`${baseUrl}/hubs/chess`)   // the cookie is sent with the connection automatically
  .withAutomaticReconnect()           // new ConnectionId after a drop, same user id (cookie)
  .build()

// Typed calls to ChessHub, generated from IChessHub.cs. Regenerate after changing the hub, its DTOs or enums:
//   cd backend && dotnet tsrts --project backend.csproj --output ../frontend/src/generated --enum Union
// (--enum Union: C# enums become "Goataro" | ... string types; Program.cs sends enums as names to match)
//
//   await chessHub.joinMatch(id)   -> Promise<GameState>
// A typo or wrong argument type is a compile error, unlike hub.invoke('JoinMatch', id).
// For what the server sends us, use getReceiverRegister('IChessClient').register(hub, {...}).
export const chessHub = getHubProxyFactory('IChessHub').createHubProxy(hub)

// HubException messages arrive as
//   "An unexpected error occurred invoking 'JoinMatch' on the server. HubException: Match is full."
// This pulls out just "Match is full.".
export function errorMessage(e: unknown): string {
  const text = e instanceof Error ? e.message : String(e)
  return text.split('HubException: ')[1] ?? text
}

let starting: Promise<void> | null = null

// Gets the session cookie, then opens the connection. Safe to call more than once.
export function connect(): Promise<void> {
  if (hub.state === signalR.HubConnectionState.Connected) return Promise.resolve()

  starting ??= (async () => {
    try {
      // The hub is [Authorize], so the cookie must exist before connecting.
      // credentials: 'include' keeps the cookie even if the backend is on another domain.
      await postSession({ credentials: 'include' })
      await hub.start()
    } finally {
      starting = null
    }
  })()

  return starting
}
