/**
 * Mémoire du mélangeur (#23, ADR 0030) : Web Audio garde chaque piste décodée en entier, en nombres flottants
 * de 4 octets, à la fréquence du contexte audio (48 kHz le plus souvent), en stéréo. 3 minutes = 66 Mo par piste.
 */
export const ASSUMED_SAMPLE_RATE = 48_000
export const BYTES_PER_SECOND = ASSUMED_SAMPLE_RATE * 2 * 4

/** 768 Mo : 8 pistes de 4 min 10 s sur ordinateur. */
export const DESKTOP_BUDGET_BYTES = 768 * 1024 * 1024
/** 256 Mo : 4 pistes de 2 min 50 s, ou 3 de 3 min 45 s, sur téléphone ou tablette. */
export const MOBILE_BUDGET_BYTES = 256 * 1024 * 1024

export interface DeviceHints {
  /** navigator.deviceMemory, en Go (Chrome et Edge seulement). */
  deviceMemory?: number
  /** Écran tactile sans souris : téléphone ou tablette. */
  coarsePointer: boolean
}

export function decodedBytes(durationsMs: readonly number[]): number {
  return durationsMs.reduce((sum, ms) => sum + (ms / 1000) * BYTES_PER_SECOND, 0)
}

export function memoryBudget(hints: DeviceHints): number {
  return hints.coarsePointer || (hints.deviceMemory ?? 8) <= 4 ? MOBILE_BUDGET_BYTES : DESKTOP_BUDGET_BYTES
}

/** Durée totale de pistes que ce budget permet, en secondes. */
export function maxSeconds(budget: number): number {
  return Math.floor(budget / BYTES_PER_SECOND)
}

export function deviceHints(): DeviceHints {
  const memory = (navigator as Navigator & { deviceMemory?: number }).deviceMemory
  return {
    deviceMemory: typeof memory === 'number' ? memory : undefined,
    coarsePointer: typeof window.matchMedia === 'function' && window.matchMedia('(pointer: coarse)').matches,
  }
}