import { describe, expect, it } from 'vitest'
import { mapProblemErrors } from '../mapProblemErrors'
import { ApiError } from '../ApiError'

describe('mapProblemErrors', () => {
  it('associe les erreurs aux champs en camelCase', () => {
    const error = new ApiError(400, { status: 400, errors: { Title: ['Obligatoire.'], '$.artist': ['Invalide.'] } })
    expect(mapProblemErrors(error)).toEqual({ title: 'Obligatoire.', artist: 'Invalide.' })
  })

  it("renvoie un objet vide pour une erreur qui n'est pas une ApiError", () => {
    expect(mapProblemErrors(new Error('x'))).toEqual({})
  })
})
