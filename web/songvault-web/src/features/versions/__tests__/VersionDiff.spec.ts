import { describe, expect, it } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import VersionDiff from '../components/VersionDiff.vue'
import type { SongVersion } from '../types'
import type { SongFile } from '@/features/files/types'

const file = (id: string, originalFileName: string): SongFile => ({
  id, originalFileName, contentType: 'audio/mpeg', sizeBytes: 1, fileType: 'Audio', uploadedAt: '2026-10-01T10:00:00Z',
})

function version(overrides: Partial<SongVersion>): SongVersion {
  return {
    id: 'v1', songId: 's', number: 1, title: 'Maquette', status: 'Demo', notes: 'Batterie à revoir',
    lyrics: 'Intro\nLa nuit tombe\nRefrain', bpm: 92, key: 'Am',
    createdAt: '2026-10-01T10:00:00Z', updatedAt: '2026-10-01T10:00:00Z', files: [file('f1', 'maquette.mp3')],
    ...overrides,
  }
}

const a = version({})
const b = version({
  id: 'v2', number: 2, status: 'Studio', lyrics: 'Intro\nLa nuit se lève\nRefrain',
  files: [file('f1', 'maquette.mp3'), file('f2', 'mix.wav')],
})

async function mountDiff(left = a, right = b) {
  const wrapper = mount(VersionDiff, { props: { a: left, b: right }, attachTo: document.body })
  await flushPromises()
  return wrapper
}

describe('VersionDiff', () => {
  it('lignes ajoutées et supprimées : balises ins et del, signe et texte, pas seulement la couleur', async () => {
    const w = await mountDiff()

    const lyrics = w.get('[aria-label="Différences : Paroles"]')
    const removed = lyrics.get('del')
    const added = lyrics.get('ins')
    expect(removed.text()).toBe('−Supprimé : La nuit tombe')
    expect(added.text()).toBe('+Ajouté : La nuit se lève')
    expect(removed.get('.sign').attributes('aria-hidden')).toBe('true')      // lu une seule fois : « Supprimé »
    expect(w.text()).toContain('1 ligne ajoutée, 1 ligne supprimée')
    w.unmount()
  })

  it('champs identiques grisés, champs modifiés marqués « Modifié » avec les deux valeurs', async () => {
    const w = await mountDiff()

    const rows = w.findAll('table.fields tbody tr')
    const status = rows.find((r) => r.text().startsWith('Statut'))!
    const bpm = rows.find((r) => r.text().startsWith('BPM'))!
    expect(status.classes()).toContain('changed')
    expect(status.text()).toContain('Démo')
    expect(status.text()).toContain('Studio')
    expect(status.text()).toContain('Modifié')
    expect(bpm.classes()).toContain('same')
    expect(bpm.text()).toContain('Identique')
    w.unmount()
  })

  it('fichiers ajoutés et retirés listés à part', async () => {
    const w = await mountDiff()

    expect(w.get('.files ins').text()).toBe('+ Ajouté : mix.wav')
    expect(w.find('.files del').exists()).toBe(false)
    w.unmount()
  })

  it('« Changement suivant » déplace le focus de changement en changement et annonce la position', async () => {
    const w = await mountDiff()
    const next = w.findAll('button').find((b) => b.text() === 'Changement suivant')!

    await next.trigger('click')
    expect(document.activeElement?.textContent).toContain('Statut')
    await next.trigger('click')
    expect(document.activeElement?.textContent).toContain('La nuit tombe')
    await next.trigger('click')
    expect(document.activeElement?.textContent).toContain('mix.wav')
    expect(w.get('[role="status"]').text()).toBe('Changement 3 sur 3')

    await next.trigger('click')                                                // revient au premier
    expect(document.activeElement?.textContent).toContain('Statut')
    w.unmount()
  })

  it('deux versions identiques : rien à parcourir', async () => {
    const w = await mountDiff(a, version({ id: 'v2', number: 2 }))

    expect(w.text()).toContain('Aucune différence.')
    expect(w.findAll('button')).toHaveLength(0)
    expect(w.find('ins').exists()).toBe(false)
    w.unmount()
  })
})