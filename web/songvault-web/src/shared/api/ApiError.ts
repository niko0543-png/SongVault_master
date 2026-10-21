import type { ProblemDetails } from './problemDetails'
import { t } from '@/shared/i18n'

export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails

  constructor(status: number, problem: ProblemDetails) {
    super(problem.detail ?? problem.title ?? t('common.errors.http', { status: String(status) }))
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
       if (error.status === 0) return t('common.errors.unreachable')
    if (error.status >= 500) {
      // #18 : la référence retrouve l'erreur et sa trace dans Application Insights
      return error.problem.reference
        ? t('common.errors.serverWithReference', { reference: error.problem.reference })
        : t('common.errors.server')
    }
    return error.message
  }
  return t('common.errors.unexpected')
}