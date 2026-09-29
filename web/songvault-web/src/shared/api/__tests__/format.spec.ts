import { describe, expect, it } from 'vitest'
import { formatDate, formatFileSize } from '../../utils/format'

describe('formatFileSize', () => {
  it.each([
    [0, '0 o'],
    [512, '512 o'],
    [1536, '1,5 Ko'],
    [5 * 1024 * 1024, '5 Mo'],
  ])('%i octets → %s', (bytes, expected) => {
    expect(formatFileSize(bytes)).toBe(expected)
  })
})

describe('formatDate', () => {
  it('formate une date ISO en français', () => {
    expect(formatDate('2026-10-12T09:00:00+00:00')).toContain('2026') // l'heure dépend du fuseau de la machine
  })
})
