import { ApiError, getErrorMessage } from '@/shared/api/ApiError'

const identityMessages: Record<string, string> = {
  PasswordTooShort: 'Le mot de passe doit contenir au moins 6 caractères.',
  PasswordRequiresDigit: 'Le mot de passe doit contenir un chiffre.',
  PasswordRequiresLower: 'Le mot de passe doit contenir une minuscule.',
  PasswordRequiresUpper: 'Le mot de passe doit contenir une majuscule.',
  PasswordRequiresNonAlphanumeric: 'Le mot de passe doit contenir un caractère spécial (!, -, #…).',
  DuplicateUserName: 'Un compte existe déjà avec cette adresse.',
  InvalidEmail: "L'adresse e-mail n'est pas valide.",
  InvalidUserName: "L'adresse e-mail n'est pas valide.",
  PasswordMismatch: 'Le mot de passe actuel est incorrect.',
  OldPasswordRequired: 'Saisissez votre mot de passe actuel.',
  InvalidToken: 'Ce lien est invalide ou a expiré : demandez-en un nouveau.',
}

/** Messages lisibles pour une erreur d'inscription. */
export function registrationErrors(error: unknown): string[] {
  if (error instanceof ApiError && error.status === 400) {
    const codes = Object.keys(error.validationErrors)
    if (codes.length > 0) return codes.map((code) => identityMessages[code] ?? error.validationErrors[code]?.[0] ?? code)
  }
  return [getErrorMessage(error)]
}

/** Message lisible pour une erreur de connexion. */
/** Connexion refusée parce que l'adresse n'est pas encore confirmée (#4). */
export function isUnconfirmed(error: unknown): boolean {
  return error instanceof ApiError && error.status === 401 && error.problem.detail?.toLowerCase() === 'notallowed'
}

/** Message lisible pour une erreur de connexion. */
export function loginError(error: unknown): string {
  if (isUnconfirmed(error))
    return 'Adresse pas encore confirmée : cliquez sur le lien reçu par e-mail, ou demandez-en un nouveau.'
  if (error instanceof ApiError && error.status === 401)
    return error.problem.detail?.toLowerCase() === 'lockedout'
        ? 'Compte verrouillé : trop de tentatives (réessayez dans quelques minutes), ou suppression du compte demandée.'
      : 'E-mail ou mot de passe incorrect.'
  if (error instanceof ApiError && error.status === 429) return 'Trop de tentatives. Réessayez dans une minute.'
  return getErrorMessage(error)
}