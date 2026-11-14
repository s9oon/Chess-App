import * as signalR from '@microsoft/signalr'
import { postSession } from './client'

// Same base URL as fetcher.ts. Unset (local dev) = relative URLs, handled by the Vite proxy.
const baseUrl = import.meta.env.VITE_API_URL ?? ''

// One connection for the whole app. Created here (not inside a component) so React
// re-rendering or StrictMode mounting twice can't open a second connection.
//
//   hub.invoke('JoinMatch', id)        → calls the public method on ChessHub
//   hub.on('OpponentJoined', () => {}) → runs when the server sends that message
//
// Hub method names are plain strings: a typo compiles fine and only fails at runtime.
export const hub = new signalR.HubConnectionBuilder()
  .withUrl(`${baseUrl}/hubs/chess`)   // the cookie is sent with the connection automatically
  .withAutomaticReconnect()           // new ConnectionId after a drop, same user id (cookie)
  .build()

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
