import { describe as suite, expect, it } from 'vitest'
import { actorLabel, describe } from '../describe'
import type { ActivityItem } from '../types'

function item(overrides: Partial<ActivityItem> = {}): ActivityItem {
  return {
    id: 1, type: 'VersionAdded', actorId: 'u-ana', actorEmail: 'ana@test.local',
    songId: 's1', songExists: true, versionId: 'v4', entityId: null,
    songTitle: 'Nocturne', versionNumber: 4, versionTitle: 'Master', fileName: null,
    createdAt: '2026-10-14T10:00:00Z', ...overrides,
  }
}

const sentence = (i: ActivityItem, me: string | null = 'u-moi') => describe(i, me).map((p) => p.text).join('')

suite('describe', () => {
  it('« ana a ajouté la v4 de Nocturne », la version en lien', () => {
    const parts = describe(item(), 'u-moi')

    expect(parts.map((p) => p.text).join('')).toBe('ana@test.local a ajouté la v4 de Nocturne')
    expect(parts[1]!.to).toEqual({ name: 'version-detail', params: { songId: 's1', versionId: 'v4' } })
  })

  it('« Vous avez… » pour ses propres modifications', () => {
    expect(sentence(item({ type: 'SongCreated' }), 'u-ana')).toBe('Vous avez créé le morceau Nocturne')
  })

  it('nomme le fichier ajouté ou commenté', () => {
    expect(sentence(item({ type: 'FileAdded', fileName: 'prise 3.wav' }))).toBe('ana@test.local a ajouté prise 3.wav à la v4 de Nocturne')
    expect(sentence(item({ type: 'CommentReplied', fileName: 'prise 3.wav' })))
      .toBe('ana@test.local a répondu à un commentaire sur prise 3.wav dans la v4 de Nocturne')
  })

  it('morceau supprimé depuis : le titre reste, sans lien', () => {
    const parts = describe(item({ type: 'SongUpdated', songExists: false }), 'u-moi')

    expect(parts.map((p) => p.text).join('')).toBe('ana@test.local a modifié le morceau Nocturne')
    expect(parts.every((p) => p.to === undefined)).toBe(true)
  })

  it('compte supprimé : « Compte supprimé », jamais « Vous »', () => {
    expect(actorLabel(item({ actorId: null, actorEmail: null }), null)).toBe('Compte supprimé')
  })
})