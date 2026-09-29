import type { ProblemDetails } from './problemDetails'

export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails

  constructor(status: number, problem: ProblemDetails) {
    super(problem.detail ?? problem.title ?? `Erreur HTTP ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.problem = problem
  }

  get validationErrors(): Record<string, string[]> {
    return this.problem.errors ?? {}
  }
}

/** Message lisible pour l'utilisateur, quelle que soit l'erreur. */
export function getErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 0) return "Le serveur est injoignable. Vérifiez que l'API est démarrée."
    if (error.status >= 500)
      return 'Le serveur est indisponible ou a rencontré une erreur. Réessayez.'
    return error.message
  }
  return 'Une erreur inattendue est survenue.'
}
