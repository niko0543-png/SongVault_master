import { describe, expect, it } from 'vitest'
import { playOrder, pointsFromTaps } from '../syncPoints'

const lookup = (...indexes: number[]) => indexes.map((index) => ({ masterBar: { index } }))

describe('playOrder (#29)', () => {
  it('numérote les passages d’une mesure reprise', () => {
    expect(playOrder(lookup(0, 1, 0, 1, 2))).toEqual([
      { barIndex: 0, occurrence: 0 },
      { barIndex: 1, occurrence: 0 },
      { barIndex: 0, occurrence: 1 },
      { barIndex: 1, occurrence: 1 },
      { barIndex: 2, occurrence: 0 },
    ])
  })
})

describe('pointsFromTaps (#29)', () => {
  const order = playOrder(lookup(0, 1, 2))

  it('une tape par début de mesure, au début de la mesure', () => {
    expect(pointsFromTaps(order, [1000.4, 2499.6])).toEqual([
      { barIndex: 0, barOccurence: 0, barPosition: 0, millisecondOffset: 1000 },
      { barIndex: 1, barOccurence: 0, barPosition: 0, millisecondOffset: 2500 },
    ])
  })

  it('ignore un double appui et les tapes après la dernière mesure', () => {
    const points = pointsFromTaps(order, [1000, 1000, 2000, 3000, 4000])
    expect(points.map((p) => [p.barIndex, p.millisecondOffset])).toEqual([[0, 1000], [1, 2000], [2, 3000]])
  })
})