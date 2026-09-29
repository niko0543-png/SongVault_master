import { ApiError } from './ApiError'
import { isProblemDetails, type ProblemDetails } from './problemDetails'

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
}

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body } = options
  const headers: Record<string, string> = { Accept: 'application/json' }
  let payload: BodyInit | undefined

  if (body instanceof FormData) {
    payload = body // upload (S4) : le navigateur fixe lui-même le Content-Type
  } else if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
    payload = JSON.stringify(body)
  }

  let response: Response
  try {
    response = await fetch(`/api${path}`, { method, headers, body: payload })
  } catch {
    throw new ApiError(0, { title: 'Serveur injoignable', status: 0 }) // réseau coupé, API arrêtée…
  }

  if (!response.ok) throw new ApiError(response.status, await readProblem(response))
  if (response.status === 204) return undefined as T
  return (await response.json()) as T // cast : non vérifié à l'exécution (cf. 11.2)
}

async function readProblem(response: Response): Promise<ProblemDetails> {
  try {
    const data: unknown = await response.json()
    if (isProblemDetails(data)) return data
  } catch {
    // corps vide ou non JSON (ex. : proxy Vite quand l'API est arrêtée)
  }
  return { title: response.statusText || `Erreur HTTP ${response.status}`, status: response.status }
}
