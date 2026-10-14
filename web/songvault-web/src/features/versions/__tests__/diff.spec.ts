import { describe, expect, it } from 'vitest'
import { changeStarts, countChanges, diffFields, diffFiles, diffLines, MAX_DIFF_CELLS } from '../diff'
import type { SongVersion } from '../types'
import type { SongFile } from '@/features/files/types'

const kinds = (before: string | null, after: string | null) =>
  diffLines(before, after).map((l) => `${l.kind === 'same' ? ' ' : l.kind === 'added' ? '+' : '-'}${l.text}`)

describe('diffLines', () => {
  it('textes identiques : aucune ligne modifiée', () => {
    expect(kinds('Refrain\nCouplet', 'Refrain\nCouplet')).toEqual([' Refrain', ' Couplet'])
  })

  it('ligne remplacée : la suppression puis l\'ajout, à sa place', () => {
    expect(kinds('Intro\nLa nuit tombe\nRefrain', 'Intro\nLa nuit se lève\nRefrain'))
      .toEqual([' Intro', '-La nuit tombe', '+La nuit se lève', ' Refrain'])
  })

  it('lignes ajoutées et supprimées au milieu', () => {
    expect(kinds('a\nb\nc\nd', 'a\nc\nx\nd'))
      .toEqual([' a', '-b', ' c', '+x', ' d'])
  })

  it('texte absent d\'un côté : tout est ajouté ou supprimé', () => {
    expect(kinds(null, 'Un\nDeux')).toEqual(['+Un', '+Deux'])
    expect(kinds('Un', '')).toEqual(['-Un'])
    expect(kinds(null, null)).toEqual([])
  })

  it('ignore la différence entre fins de ligne Windows et Unix', () => {
    expect(kinds('Un\r\nDeux', 'Un\nDeux')).toEqual([' Un', ' Deux'])
  })

  it('un texte énorme ne bloque pas la page : tout remplacé, sans calcul', () => {
    const size = Math.ceil(Math.sqrt(MAX_DIFF_CELLS)) + 1
    const before = Array.from({ length: size }, (_, i) => `a${i}`).join('\n')
    const after = Array.from({ length: size }, (_, i) => `b${i}`).join('\n')

    expect(countChanges(diffLines(before, after))).toEqual({ added: size, removed: size })
  })
})

describe('countChanges et changeStarts', () => {
  it('compte les lignes et repère le début de chaque bloc de changements', () => {
    const lines = diffLines('a\nb\nc\nd\ne', 'a\nB\nc\nd\nE\nF')

    expect(countChanges(lines)).toEqual({ added: 3, removed: 2 })
    expect(changeStarts(lines)).toEqual([1, 5])
  })
})

function version(overrides: Partial<SongVersion>): SongVersion {
  return {
    id: 'v', songId: 's', number: 1, title: 'Maquette', status: 'Demo', notes: null, lyrics: null,
    bpm: 92, key: 'Am', createdAt: '2026-10-01T10:00:00Z', updatedAt: '2026-10-01T10:00:00Z', files: [],
    ...overrides,
  }
}

describe('diffFields', () => {
  it('marque les champs modifiés et affiche « — » pour une valeur absente', () => {
    const fields = diffFields(version({}), version({ status: 'Studio', bpm: null }))

    expect(fields.map((f) => [f.label, f.before, f.after, f.changed])).toEqual([
      ['Titre', 'Maquette', 'Maquette', false],
      ['Statut', 'Démo', 'Studio', true],
      ['BPM', '92', '—', true],
      ['Tonalité', 'Am', 'Am', false],
    ])
  })
})

describe('diffFiles', () => {
  const file = (id: string, originalFileName: string): SongFile => ({
    id, originalFileName, contentType: 'audio/mpeg', sizeBytes: 1, fileType: 'Audio', uploadedAt: '2026-10-01T10:00:00Z',
  })

  it('compare les fichiers par nom, sans tenir compte de la casse', () => {
    const diff = diffFiles(
      [file('1', 'maquette.mp3'), file('2', 'grille.pdf')],
      [file('3', 'Maquette.MP3'), file('4', 'mix-v2.wav')],
    )

    expect(diff.added.map((f) => f.originalFileName)).toEqual(['mix-v2.wav'])
    expect(diff.removed.map((f) => f.originalFileName)).toEqual(['grille.pdf'])
    expect(diff.kept.map((f) => f.originalFileName)).toEqual(['Maquette.MP3'])
  })
})