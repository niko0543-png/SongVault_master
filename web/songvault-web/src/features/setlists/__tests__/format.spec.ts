import { describe, expect, it } from 'vitest'
import { formatDuration, formatSetlistDate, formatTotal, parseDuration } from '../format'

describe('format des setlists', () => {
  it('écrit la durée d\'un morceau en minutes:secondes, arrondie à la seconde', () => {
    expect(formatDuration(187_431)).toBe('3:07')
    expect(formatDuration(59_600)).toBe('1:00')
    expect(formatDuration(null)).toBe('')
  })

  it('écrit le total avec les heures au-delà de 60 minutes', () => {
    expect(formatTotal(2_537_000)).toBe('42 min 17 s')
    expect(formatTotal(3_725_000)).toBe('1 h 02 min 05 s')
    expect(formatTotal(0)).toBe('0 min 00 s')
  })

  it('lit "m:ss" ou des minutes seules, et refuse le reste', () => {
    expect(parseDuration(' 3:45 ')).toBe(225_000)
    expect(parseDuration('4')).toBe(240_000)
    expect(parseDuration('')).toBeNull()
    expect(parseDuration('3:75')).toBeUndefined()
    expect(parseDuration('trois')).toBeUndefined()
    expect(parseDuration('0:00')).toBeUndefined()      // moins d'une seconde
    expect(parseDuration('61')).toBeUndefined()        // plus de 60 minutes
  })

  it('écrit la date en toutes lettres', () => {
    expect(formatSetlistDate('2026-11-28')).toBe('samedi 28 novembre 2026')
    expect(formatSetlistDate(null)).toBe('Sans date')
  })
})