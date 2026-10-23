import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, RouterLinkStub } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import SongReleaseSection from '../components/SongReleaseSection.vue'
import StreamingLinkList from '../components/StreamingLinkList.vue'
import { releasesApi } from '../api/releasesApi'
import type { BandRole } from '@/features/bands/types'
import type { Release } from '../types'

vi.mock('../api/releasesApi', () => ({
  releasesApi: { list: vi.fn(), get: vi.fn(), releaseSong: vi.fn(), unreleaseSong: vi.fn() },
}))

const ep: Release = {
  id: 'ep1', title: 'Nuits blanches', type: 'EP', releaseDate: '2026-11-20', hasCover: false,
  createdAt: '2026-10-14T10:00:00Z', updatedAt: '2026-10-14T10:00:00Z', tracks: [],
  label: 'Nuit Records', upc: '036000291452',
  streamingLinks: [{ platform: 'Spotify', url: 'https://open.spotify.com/album/1' }],
}

type Props = InstanceType<typeof SongReleaseSection>['$props']

async function mountSection(props: Partial<Props> = {}, role: BandRole = 'Owner') {
  const w = mount(SongReleaseSection, {
    props: {
      songId: 's1', songTitle: 'Nocturne', versionId: 'v3', versionNumber: 3, versionStatus: 'Final',
      isLatest: true, otherVersionCount: 2, releaseId: null, ...props,
    },
    global: {
      plugins: [createTestingPinia({
        createSpy: vi.fn,
        stubActions: false,                                  // bands.can() doit répondre
        initialState: { band: { bands: [{ id: 'b1', name: 'Les Rats', role }], activeId: 'b1', isLoaded: true } },
      })],
      stubs: { RouterLink: RouterLinkStub },
    },
  })
  await flushPromises()
  return w
}

describe('SongReleaseSection (#38)', () => {
  beforeEach(() => {
    vi.mocked(releasesApi.list).mockReset().mockResolvedValue([
      { id: 'ep1', title: 'Nuits blanches', type: 'EP', releaseDate: null, hasCover: false, trackCount: 3 },
    ])
    vi.mocked(releasesApi.get).mockReset().mockResolvedValue(ep)
    vi.mocked(releasesApi.releaseSong).mockReset().mockResolvedValue({
      releaseId: 'r-new', removedVersions: [1], keptVersions: [{ number: 2, releases: [], setlists: ['Concert du 12'] }], purged: false,
    })
    vi.mocked(releasesApi.unreleaseSong).mockReset().mockResolvedValue(undefined)
  })

  it('enregistre les infos puis demande quoi garder avant d’envoyer', async () => {
    const w = await mountSection()
    await w.get('#sr-date').setValue('2026-12-01')
    await w.get('#sr-label').setValue('Nuit Records')
    await w.get('#sr-link-0').setValue(' https://open.spotify.com/track/1 ')
    await w.get('form').trigger('submit')

    expect(releasesApi.releaseSong).not.toHaveBeenCalled()
    expect(w.get('[data-testid="keep-versions"]').text()).toContain('Ne garder que la v3')
    await w.get('input[value="LatestOnly"]').setValue()
    await w.get('form').trigger('submit')
    await flushPromises()

    expect(releasesApi.releaseSong).toHaveBeenCalledWith('s1', {
      versionId: 'v3', releaseId: null, releaseDate: '2026-12-01', label: 'Nuit Records', upc: null,
      streamingLinks: ['https://open.spotify.com/track/1'], keep: 'LatestOnly', purgeNow: false,
    })
    expect(w.emitted('released')?.[0]).toEqual([expect.objectContaining({ releaseId: 'r-new' })])
  })

  it('le propriétaire peut libérer la place tout de suite ; un membre ne le voit pas', async () => {
    const owner = await mountSection()
    await owner.get('form').trigger('submit')
    expect(owner.find('input[type="checkbox"]').exists()).toBe(false)       // pas tant qu'on garde tout
    await owner.get('input[value="LatestOnly"]').setValue()
    await owner.get('input[type="checkbox"]').setValue(true)
    await owner.get('form').trigger('submit')
    await flushPromises()
    expect(vi.mocked(releasesApi.releaseSong).mock.calls[0]![1]).toMatchObject({ keep: 'LatestOnly', purgeNow: true })

    const member = await mountSection({}, 'Member')
    await member.get('form').trigger('submit')
    await member.get('input[value="LatestOnly"]').setValue()
    expect(member.find('input[type="checkbox"]').exists()).toBe(false)
  })

  it('une seule version : pas de question, elle sort telle quelle', async () => {
    const w = await mountSection({ otherVersionCount: 0 })
    await w.get('form').trigger('submit')
    await flushPromises()

    expect(w.find('[data-testid="keep-versions"]').exists()).toBe(false)
    expect(vi.mocked(releasesApi.releaseSong).mock.calls[0]![1]).toMatchObject({ keep: 'All', purgeNow: false })
  })

  it('choisir une sortie existante reprend ses infos de sortie', async () => {
    const w = await mountSection()
    await w.get('#sr-release').setValue('ep1')
    await flushPromises()

    expect((w.get('#sr-label').element as HTMLInputElement).value).toBe('Nuit Records')
    expect((w.get('#sr-upc').element as HTMLInputElement).value).toBe('036000291452')
    expect((w.get('#sr-link-0').element as HTMLInputElement).value).toBe('https://open.spotify.com/album/1')
  })

  it('pas encore Final : explique comment faire ; pas la dernière version : rien', async () => {
    expect((await mountSection({ versionStatus: 'Studio' })).text()).toContain('statut Final')
    expect((await mountSection({ isLatest: false })).html()).not.toContain('section')
  })

  it('morceau sorti : sa sortie, ses liens et « Remettre dans les morceaux »', async () => {
    const w = await mountSection({ releaseId: 'ep1' })

    expect(w.text()).toContain('Nuits blanches')
    expect(w.text()).toContain('Nuit Records')
    expect(w.get('[data-testid="streaming-link"]').text()).toContain('Spotify')
    await w.get('button').trigger('click')
    await flushPromises()
    expect(releasesApi.unreleaseSong).toHaveBeenCalledWith('s1')
    expect(w.emitted('unreleased')).toHaveLength(1)
  })
})

describe('StreamingLinkList (#38)', () => {
  it('nomme la plateforme, ou le site quand elle est inconnue, et ouvre un nouvel onglet', () => {
    const w = mount(StreamingLinkList, {
      props: { links: [
        { platform: 'AppleMusic', url: 'https://music.apple.com/fr/album/1' },
        { platform: 'Other', url: 'https://www.lesnuits.fr/ecouter' },
      ] },
    })
    const links = w.findAll('a')
    expect(links.map((a) => a.text())).toEqual(['▶ Apple Music', '▶ lesnuits.fr'])
    expect(links[0]!.attributes()).toMatchObject({ target: '_blank', rel: 'noopener noreferrer' })
  })
})
