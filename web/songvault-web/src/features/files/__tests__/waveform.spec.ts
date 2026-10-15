import { describe, expect, it } from 'vitest'
import { waveformBars, waveformPath } from '../waveform'

describe('waveformBars', () => {
  it('garde la crête la plus haute de chaque groupe', () => {
    expect(waveformBars([1, 9, 3, 4, 255, 0], 3)).toEqual([9, 4, 255])
  })

  it("ne crée pas plus de barres qu'il n'y a de crêtes", () => {
    expect(waveformBars([10, 20], 200)).toEqual([10, 20])
    expect(waveformBars([], 200)).toEqual([])
  })

  it('réduit 1000 crêtes à 200 barres', () => {
    const peaks = Array.from({ length: 1000 }, (_, i) => i % 256)
    expect(waveformBars(peaks, 200)).toHaveLength(200)
  })
})

describe('waveformPath', () => {
  it('centre chaque barre et garde au moins 1 de hauteur pour le silence', () => {
    expect(waveformPath([255, 0])).toBe('M0 0h0.6v40h-0.6zM1 19.5h0.6v1h-0.6z')
  })
})