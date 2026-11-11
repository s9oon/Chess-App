// Every Orval-generated call goes through here, so the backend URL lives in one place.
// VITE_API_URL is baked in at build time. Unset (local dev) = relative URLs, handled by the Vite proxy.
const baseUrl = import.meta.env.VITE_API_URL ?? ''

export const customFetch = async <T>(url: string, options: RequestInit): Promise<T> => {
  const res = await fetch(`${baseUrl}${url}`, options)
  const body = [204, 205, 304].includes(res.status) ? null : await res.text()
  const data = body ? JSON.parse(body) : undefined
  return { data, status: res.status, headers: res.headers } as T
}
