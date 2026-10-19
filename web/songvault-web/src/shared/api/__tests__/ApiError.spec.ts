import { describe, expect, it } from 'vitest'
import { ApiError, getErrorMessage } from '../ApiError'

describe('getErrorMessage', () => {
  it('erreur 500 : donne la référence de l’erreur quand l’API en fournit une', () => {
    const error = new ApiError(500, { status: 500, title: 'Une erreur interne est survenue', reference: '4bf92f3577b34da6a3ce929d0e0e4736' })

    expect(getErrorMessage(error)).toBe(
      'Le serveur est indisponible ou a rencontré une erreur. Réessayez. Référence : 4bf92f3577b34da6a3ce929d0e0e4736',
    )
  })

  it('erreur 502 de nginx, sans référence : le message habituel', () => {
    expect(getErrorMessage(new ApiError(502, { status: 502, title: 'Bad Gateway' }))).toBe(
      'Le serveur est indisponible ou a rencontré une erreur. Réessayez.',
    )
  })

  it('erreur attendue : le message de l’API', () => {
    expect(getErrorMessage(new ApiError(409, { status: 409, detail: 'Analyse antivirus en cours.' }))).toBe(
      'Analyse antivirus en cours.',
    )
  })
})