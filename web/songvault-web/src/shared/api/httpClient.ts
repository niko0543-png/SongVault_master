import { ApiError } from './ApiError'
import { isProblemDetails, type ProblemDetails } from './problemDetails'

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
}

// ---- Réaction à une session expirée (branchée dans main.ts) ----
let onUnauthorized: (() => void) | null = null

export function setUnauthorizedHandler(handler: (() => void) | null) {
  onUnauthorized = handler
}

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body } = options
  const headers: Record<string, string> = { Accept: 'application/json' }
  let payload: BodyInit | undefined

  if (body instanceof FormData) {
    payload = body
  } else if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
    payload = JSON.stringify(body)
  }

  let response: Response
  try {
    response = await fetch(`/api${path}`, { method, headers, body: payload })
  } catch {
    throw new ApiError(0, { title: 'Serveur injoignable', status: 0 })
  }

  if (!response.ok) {
    // Les routes /auth/ gèrent elles-mêmes leurs 401 (mauvais mot de passe, « pas connecté »)
    if (response.status === 401 && !path.startsWith('/auth/')) onUnauthorized?.()
    throw new ApiError(response.status, await readProblem(response))
  }

  if (response.status === 204) return undefined as T
  const text = await response.text()                 // corps éventuellement vide (ex. login Identity)
  return (text ? JSON.parse(text) : undefined) as T
}

async function readProblem(response: Response): Promise<ProblemDetails> {
  try {
    const data: unknown = await response.json()
    if (isProblemDetails(data)) return data
  } catch {
    // corps vide ou non JSON
  }
  return { title: response.statusText || `Erreur HTTP ${response.status}`, status: response.status }
}