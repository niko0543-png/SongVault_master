import { describe, expect, it } from 'vitest'
import { defaultPair, parseVersionNumber } from '../compare'

describe('parseVersionNumber', () => {
  it.each([
    ['3', 3], [['2', '5'], 2], [undefined, null], ['abc', null], ['0', null], ['-1', null], ['1.5', null],
  ])('%j → %j', (input, expected) => {
    expect(parseVersionNumber(input)).toBe(expected)
  })
})

describe('defaultPair', () => {
  it('choisit les deux versions les plus récentes', () => expect(defaultPair([1, 4, 2, 3])).toEqual([3, 4]))
  it('renvoie null avec moins de deux versions', () => expect(defaultPair([1])).toBeNull())
})
