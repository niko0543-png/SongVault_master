/** Format d'erreur renvoyé par l'API (RFC 9457), cf. GlobalExceptionHandler. */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  traceId?: string
  errors?: Record<string, string[]> // présent pour les 400 de validation
}

/** Garde de type : vérifie à l'exécution ce que TypeScript ne peut pas vérifier. */
export function isProblemDetails(value: unknown): value is ProblemDetails {
  return typeof value === 'object' && value !== null && ('title' in value || 'status' in value)
}
