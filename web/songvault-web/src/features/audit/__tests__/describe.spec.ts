import { describe as suite, expect, it } from 'vitest'
import { actorLabel, describe } from '../describe'
import type { AuditItem } from '../types'

function item(over: Partial<AuditItem>): AuditItem {
  return {
    id: 1, action: 'SongDeleted', actorId: 'u-ana', actorEmail: 'ana@test.local',
    subject: 'Nocturne', targetUserId: null, targetEmail: null, details: null, occurredAt: '2026-10-19T09:00:00Z',
    ...over,
  }
}

suite('describe (journal d’audit)', () => {
  it('nomme l’auteur : vous, son adresse, SongVault ou un compte supprimé', () => {
    expect(actorLabel(item({}), 'u-moi')).toBe('ana@test.local')
    expect(actorLabel(item({ actorId: 'u-moi' }), 'u-moi')).toBe('Vous')
    expect(actorLabel(item({ actorId: null, actorEmail: null }), 'u-moi')).toBe('SongVault')
    expect(actorLabel(item({ actorEmail: null }), 'u-moi')).toBe('Compte supprimé')
  })

  it('écrit une phrase par action', () => {
    expect(describe(item({}), 'u-moi')).toBe('ana@test.local a supprimé le morceau Nocturne')
    expect(describe(item({ actorId: 'u-moi', action: 'MemberRemoved', targetUserId: 'u-bob', targetEmail: 'bob@test.local' }), 'u-moi'))
      .toBe('Vous avez retiré bob@test.local du groupe')
    expect(describe(item({ action: 'MemberRoleChanged', targetUserId: 'u-moi', targetEmail: 'moi@test.local' }), 'u-moi'))
      .toBe('ana@test.local a changé le rôle de vous')
    expect(describe(item({ action: 'CommentDeleted', subject: 'Réponse', targetUserId: 'u-x', targetEmail: null }), 'u-moi'))
      .toBe('ana@test.local a supprimé une réponse de un compte supprimé')
    expect(describe(item({ action: 'InvitationCreated', subject: 'b***@test.local' }), 'u-moi'))
      .toBe('ana@test.local a invité b***@test.local')
    expect(describe(item({ action: 'MemberLeft', actorId: 'u-moi' }), 'u-moi')).toBe('Vous avez quitté le groupe')
    expect(describe(item({ action: 'FileQuarantined', actorId: null, subject: 'Nocturne — v2 — mix.wav' }), 'u-moi'))
      .toBe('L\'antivirus a bloqué le fichier Nocturne — v2 — mix.wav')
  })
})