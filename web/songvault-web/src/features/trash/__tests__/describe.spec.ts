import { describe, expect, it } from 'vitest'
import { daysLeft, trashLabel } from '../describe'
import type { TrashItem } from '../types'

const base: TrashItem = {
  kind: 'Song', id: 's1', songId: 's1', songTitle: 'Nocturne', versionNumber: null, versionTitle: null, fileName: null,
  sizeBytes: 0, deletedAt: '2026-10-19T09:00:00Z', deletedById: null, deletedByEmail: null, purgeAt: '2026-11-18T09:00:00Z',
}

describe('corbeille', () => {
  it('nomme un morceau, une version et un fichier', () => {
    expect(trashLabel(base)).toBe('Nocturne')
    expect(trashLabel({ ...base, kind: 'Version', versionNumber: 2, versionTitle: 'Maquette' })).toBe('Nocturne — v2 Maquette')
    expect(trashLabel({ ...base, kind: 'File', versionNumber: 2, fileName: 'prise.wav' })).toBe('Nocturne — v2 — prise.wav')
  })

  it('compte les jours avant la purge', () => {
    expect(daysLeft(base, new Date('2026-10-19T09:00:00Z'))).toBe(30)
    expect(daysLeft(base, new Date('2026-11-17T10:00:00Z'))).toBe(1)
    expect(daysLeft(base, new Date('2026-11-19T00:00:00Z'))).toBe(0)
  })
})