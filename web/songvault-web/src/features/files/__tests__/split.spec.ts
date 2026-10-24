import { describe, expect, it } from 'vitest'
import {
  cutAt, formatCutTime, mergeWithNext, moveCut, splitProblem, SPLIT_MAX_PARTS, toSplitRequest, wholeFile, type SplitSegment,
} from '../split'

const HOUR = 3_600_000
const bounds = (segments: SplitSegment[]) => segments.map((s) => [s.startMs, s.endMs])

describe('découpage, coupures (ADR 0050)', () => {
  it('coupe le passage qui contient la position, le premier gardant ses réglages', () => {
    const start = wholeFile(HOUR, 'Intro')
    start[0]!.songId = 'song-1'

    const cut = cutAt(cutAt(start, 600_000), 300_000.4)

    expect(bounds(cut)).toEqual([[0, 300_000], [300_000, 600_000], [600_000, HOUR]])
    expect(cut[0]).toMatchObject({ title: 'Intro', songId: 'song-1', keep: true })
    expect(cut[1]).toMatchObject({ title: '', songId: null, keep: true })
  })

  it("ne coupe pas à moins d'une seconde d'une coupure ou d'un bord", () => {
    const once = cutAt(wholeFile(HOUR), 600_000)

    expect(cutAt(once, 600_500)).toBe(once)
    expect(cutAt(once, 400)).toBe(once)
    expect(cutAt(once, HOUR - 999)).toBe(once)
  })

  it('retire une coupure en gardant les réglages du premier passage', () => {
    const cut = cutAt(cutAt(wholeFile(HOUR, 'A'), 600_000), 1_200_000)

    const merged = mergeWithNext(cut, 0)

    expect(bounds(merged)).toEqual([[0, 1_200_000], [1_200_000, HOUR]])
    expect(merged[0]!.title).toBe('A')
    expect(mergeWithNext(merged, 1)).toBe(merged)          // pas de passage après le dernier
  })

  it("déplace une coupure sans laisser de passage de moins d'une seconde", () => {
    const cut = cutAt(cutAt(wholeFile(HOUR), 600_000), 1_200_000)

    expect(bounds(moveCut(cut, 0, 601_000))[0]).toEqual([0, 601_000])
    expect(bounds(moveCut(cut, 0, 1_200_000))[1]).toEqual([1_199_000, 1_200_000])
  })
})

describe('découpage, envoi', () => {
  it("n'envoie que les passages gardés, titre nettoyé, morceau existant s'il est choisi", () => {
    const cut = cutAt(cutAt(wholeFile(HOUR, ' Intro '), 600_000), 1_200_000)
    cut[1]!.keep = false
    cut[2]!.title = 'Nocturne'
    cut[2]!.songId = 'song-9'

    expect(toSplitRequest(cut)).toEqual({
      parts: [
        { startMs: 0, endMs: 600_000, title: 'Intro' },
        { startMs: 1_200_000, endMs: HOUR, title: 'Nocturne', songId: 'song-9' },
      ],
    })
  })

  it('dit ce qui manque avant de pouvoir envoyer', () => {
    const one = wholeFile(HOUR, 'A')
    expect(splitProblem(one)).toBeNull()
    expect(splitProblem([{ ...one[0]!, keep: false }])).toBe('none')
    expect(splitProblem([{ ...one[0]!, title: '  ' }])).toBe('title')
    const many = Array.from({ length: SPLIT_MAX_PARTS + 1 }, (_, i) =>
      ({ startMs: i * 2000, endMs: i * 2000 + 2000, keep: true, title: `P${i}`, songId: null }))
    expect(splitProblem(many)).toBe('tooMany')
  })

  it('affiche les instants au dixième de seconde', () => {
    expect(formatCutTime(0)).toBe('0:00.0')
    expect(formatCutTime(65_430)).toBe('1:05.4')
    expect(formatCutTime(3_723_000)).toBe('1:02:03.0')
  })
})
