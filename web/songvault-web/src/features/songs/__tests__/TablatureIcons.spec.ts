import { describe, expect, it } from 'vitest'
import { mount, RouterLinkStub } from '@vue/test-utils'
import SongCard from '../components/SongCard.vue'
import VersionTimeline from '@/features/versions/components/VersionTimeline.vue'
import type { Song } from '../types'

/** #34 : l'icône 🎸 ouvre la tablature depuis la liste des morceaux et celle des versions. */
const song: Song = {
  id: 's1', title: 'Nocturne', artist: null, description: null,
  createdAt: '2026-10-12T09:00:00Z', updatedAt: '2026-10-12T09:00:00Z', badge: 'None',
}
const stubs = { stubs: { RouterLink: RouterLinkStub } }

describe('Icône de tablature (#34)', () => {
  it('dans la liste des morceaux, mène à la tablature de la dernière version qui en a une', () => {
    const withTab = mount(SongCard, { props: { song: { ...song, tablatureVersionId: 'v2', tablatureFileId: 'f9' } }, global: stubs })
    const link = withTab.get('[data-testid="song-tablature"]')
    expect(link.attributes('aria-label')).toBe('Ouvrir la tablature de Nocturne')
    expect(withTab.getComponent(RouterLinkStub).props('to'))
      .toEqual({ name: 'tablature', params: { songId: 's1', versionId: 'v2', fileId: 'f9' } })

    const without = mount(SongCard, { props: { song: { ...song, tablatureVersionId: null, tablatureFileId: null } }, global: stubs })
    expect(without.find('[data-testid="song-tablature"]').exists()).toBe(false)
  })

  it('dans la liste des versions, une icône par version qui a une tablature', () => {
    const versions = [
      { id: 'v1', number: 1, title: 'Idée', status: 'Idea' as const, createdAt: '2026-10-12T09:00:00Z', tablatureFileId: null },
      { id: 'v2', number: 2, title: 'Démo', status: 'Demo' as const, createdAt: '2026-10-13T09:00:00Z', tablatureFileId: 'f9' },
    ]
    const wrapper = mount(VersionTimeline, { props: { songId: 's1', versions, readonly: true }, global: stubs })
    const icons = wrapper.findAll('[data-testid="version-tablature"]')
    expect(icons).toHaveLength(1)
    expect(icons[0]!.attributes('aria-label')).toBe('Ouvrir la tablature de la version 2')
  })
})
