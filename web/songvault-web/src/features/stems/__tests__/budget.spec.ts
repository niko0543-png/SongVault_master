import { describe, expect, it } from 'vitest'
import { DESKTOP_BUDGET_BYTES, MOBILE_BUDGET_BYTES, decodedBytes, maxSeconds, memoryBudget } from '../budget'

describe('mémoire du mélangeur', () => {
  it('compte 384 000 octets par seconde de piste décodée (48 kHz, stéréo, 4 octets)', () => {
    expect(decodedBytes([180_000])).toBe(69_120_000)
    expect(decodedBytes([1000, 2000])).toBe(1_152_000)
  })

  it('garde le petit budget sur un écran tactile ou avec 4 Go de mémoire au plus', () => {
    expect(memoryBudget({ coarsePointer: true, deviceMemory: 8 })).toBe(MOBILE_BUDGET_BYTES)
    expect(memoryBudget({ coarsePointer: false, deviceMemory: 4 })).toBe(MOBILE_BUDGET_BYTES)
    expect(memoryBudget({ coarsePointer: false })).toBe(DESKTOP_BUDGET_BYTES)
  })

  it('permet 8 pistes de 4 minutes sur ordinateur, mais pas sur téléphone', () => {
    const eight = decodedBytes(Array.from({ length: 8 }, () => 240_000))
    expect(eight).toBeLessThanOrEqual(DESKTOP_BUDGET_BYTES)
    expect(eight).toBeGreaterThan(MOBILE_BUDGET_BYTES)
    expect(maxSeconds(MOBILE_BUDGET_BYTES)).toBe(699)
  })
})