import { describe, expect, it } from 'vitest'
import { isUnconfirmed, loginError, registrationErrors } from '../messages'
import { ApiError } from '@/shared/api/ApiError'

const unauthorized = (detail: string) => new ApiError(401, { title: 'Unauthorized', status: 401, detail })

describe('messages du compte', () => {
  it('reconnaît une adresse non confirmée, et seulement elle', () => {
    expect(isUnconfirmed(unauthorized('NotAllowed'))).toBe(true)
    expect(loginError(unauthorized('NotAllowed'))).toContain('pas encore confirmée')
    expect(isUnconfirmed(unauthorized('Failed'))).toBe(false)
    expect(loginError(unauthorized('Failed'))).toBe('E-mail ou mot de passe incorrect.')
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