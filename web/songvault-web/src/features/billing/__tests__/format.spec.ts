import { describe, expect, it } from 'vitest'
import { describeBands, formatMonthlyPrice, usageRatio } from '../format'

describe('format des formules', () => {
  it('prix mensuel en euros, ou « Gratuit »', () => {
    expect(formatMonthlyPrice(600)).toBe('6,00 € par mois')
    expect(formatMonthlyPrice(0)).toBe('Gratuit')
  })

  it('nombre de groupes', () => {
    const plan = { id: 'x', name: 'X', storageQuotaBytes: 1, monthlyPriceCents: 0 }
    expect(describeBands({ ...plan, maxBandsPerOwner: 1 })).toBe('Un groupe dont vous êtes propriétaire')
    expect(describeBands({ ...plan, maxBandsPerOwner: null })).toBe('Autant de groupes que vous voulez')
  })

  it('jauge bornée, même au-delà du quota après une baisse de formule', () => {
    expect(usageRatio(500, 1000)).toBe(0.5)
    expect(usageRatio(5000, 1000)).toBe(1)
  })
})