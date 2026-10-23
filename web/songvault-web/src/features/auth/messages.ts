import { ApiError, getErrorMessage } from '@/shared/api/ApiError'
import { labels, t } from '@/shared/i18n'

const identityMessages: Readonly<Record<string, string>> = labels({
  PasswordTooShort: 'auth.identity.passwordTooShort',
  PasswordRequiresDigit: 'auth.identity.requiresDigit',
  PasswordRequiresLower: 'auth.identity.requiresLower',
  PasswordRequiresUpper: 'auth.identity.requiresUpper',
  PasswordRequiresNonAlphanumeric: 'auth.identity.requiresNonAlphanumeric',
  DuplicateUserName: 'auth.identity.duplicateUserName',
  InvalidEmail: 'auth.identity.invalidEmail',
  InvalidUserName: 'auth.identity.invalidEmail',
  PasswordMismatch: 'auth.identity.passwordMismatch',
  OldPasswordRequired: 'auth.identity.oldPasswordRequired',
  InvalidToken: 'auth.identity.invalidToken',
})

/** Messages lisibles pour une erreur d'inscription. */
export function registrationErrors(error: unknown): string[] {
  if (error instanceof ApiError && error.status === 400) {
    const codes = Object.keys(error.validationErrors)
    if (codes.length > 0) return codes.map((code) => identityMessages[code] ?? error.validationErrors[code]?.[0] ?? code)
  }
  return [getErrorMessage(error)]
}

/** Connexion refusée parce que l'adresse n'est pas encore confirmée (#4). */
export function isUnconfirmed(error: unknown): boolean {
  return error instanceof ApiError && error.status === 401 && error.problem.detail?.toLowerCase() === 'notallowed'
}

/** Bon mot de passe, double authentification active : il faut encore le code (#19). */
export function requiresTwoFactor(error: unknown): boolean {
  return error instanceof ApiError && error.status === 401 && error.problem.detail?.toLowerCase() === 'requirestwofactor'
}

/** Message lisible pour une erreur de connexion ; withCode : le code venait d'être saisi (#19). */
export function loginError(error: unknown, withCode = false): string {
  if (isUnconfirmed(error)) return t('auth.loginErrors.unconfirmed')
  if (error instanceof ApiError && error.status === 401)
    return error.problem.detail?.toLowerCase() === 'lockedout' ? t('auth.loginErrors.lockedOut')
      : withCode ? t('auth.loginErrors.wrongCode')
      : t('auth.loginErrors.wrongCredentials')
  if (error instanceof ApiError && error.status === 429) return t('auth.loginErrors.tooManyAttempts')
  return getErrorMessage(error)
}