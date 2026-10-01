import { ApiError } from './ApiError'

/**
 * Transforme les erreurs de validation de l'API en { champ: premier message }.
 * "Title" → "title" ; "$.title" (JSON invalide) → "title".
 */
export function mapProblemErrors(error: unknown): Record<string, string> {
  if (!(error instanceof ApiError)) return {}

  const result: Record<string, string> = {}
  for (const [key, messages] of Object.entries(error.validationErrors)) {
    const field = normalize(key)
    const first = messages[0]
    if (first && !(field in result)) result[field] = first
  }
  return result
}

function normalize(key: string): string {
  const k = key.replace(/^\$\./, '')
  return k.charAt(0).toLowerCase() + k.slice(1)
}
