import { describe, expect, it } from 'vitest'
import { isUnconfirmed, loginError, registrationErrors, requiresTwoFactor } from '../messages'
import { ApiError } from '@/shared/api/ApiError'

const unauthorized = (detail: string) => new ApiError(401, { title: 'Unauthorized', status: 401, detail })

describe('messages du compte', () => {
  it('reconnaît une adresse non confirmée, et seulement elle', () => {
    expect(isUnconfirmed(unauthorized('NotAllowed'))).toBe(true)
    expect(loginError(unauthorized('NotAllowed'))).toContain('pas encore confirmée')
    expect(isUnconfirmed(unauthorized('Failed'))).toBe(false)
    expect(loginError(unauthorized('Failed'))).toBe('E-mail ou mot de passe incorrect.')
  })

  it('#19 : reconnaît l’étape du code, et un code refusé', () => {
    expect(requiresTwoFactor(unauthorized('RequiresTwoFactor'))).toBe(true)
    expect(requiresTwoFactor(unauthorized('Failed'))).toBe(false)
    expect(loginError(unauthorized('Failed'), true)).toContain('Code incorrect')
    expect(loginError(unauthorized('LockedOut'), true)).toContain('Compte verrouillé')
  })

  it('traduit un lien expiré et un mauvais mot de passe actuel', () => {
    const error = new ApiError(400, {
      title: 'One or more validation errors occurred.', status: 400,
      errors: { InvalidToken: ['Invalid token.'], PasswordMismatch: ['Incorrect password.'] },
    })
    expect(registrationErrors(error)).toEqual([
      'Ce lien est invalide ou a expiré : demandez-en un nouveau.',
      'Le mot de passe actuel est incorrect.',
    ])
  })
})