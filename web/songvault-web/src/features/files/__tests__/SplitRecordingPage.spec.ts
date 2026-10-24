import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, RouterLinkStub } from '@vue/test-utils'
import SplitRecordingPage from '../pages/SplitRecordingPage.vue'
import { filesApi } from '../api/filesApi'
import type { AudioSplit } from '../types'

const role = vi.hoisted(() => ({ canWrite: true }))
vi.mock('@/features/bands/stores/bandStore', () => ({ useBandStore: () => ({ can: () => role.canWrite }) }))
vi.mock('../api/filesApi', () => ({
  filesApi: { listenUrl: () => '/listen', waveform: vi.fn(), latestSplit: vi.fn(), split: vi.fn() },
}))
vi.mock('@/features/versions/api/versionsApi', () => ({
  versionsApi: {
    get: vi.fn().mockResolvedValue({
      id: 'v1', number: 1, title: 'Répétition du 24/10/2026',
      files: [{ id: 'f1', originalFileName: 'repet.wav' }],
    }),
  },
}))
vi.mock('@/features/songs/api/songsApi', () => ({
  songsApi: {
    list: vi.fn().mockResolvedValue({
      items: [{ id: 'song-n', title: 'Nocturne' }, { id: 'song-a', title: 'Aube' }],
      page: 1, pageSize: 50, totalCount: 2, totalPages: 1,
    }),
  },
}))

const HOUR = 3_600_000

async function mountPage() {
  const wrapper = mount(SplitRecordingPage, {
    props: { songId: 's1', versionId: 'v1', fileId: 'f1' },
    global: { stubs: { RouterLink: RouterLinkStub } },
  })
  await flushPromises()
  return wrapper
}

/** Avance la lecture : la page suit currentTime de son <audio>. */
async function playTo(wrapper: Awaited<ReturnType<typeof mountPage>>, ms: number) {
  const audio = wrapper.get('audio').element as HTMLAudioElement
  Object.defineProperty(audio, 'currentTime', { value: ms / 1000, writable: true, configurable: true })
  await wrapper.get('audio').trigger('timeupdate')
}

describe('SplitRecordingPage (ADR 0050)', () => {
  beforeEach(() => {
    role.canWrite = true
    vi.mocked(filesApi.waveform).mockResolvedValue({ durationMs: HOUR, peaks: Array.from({ length: 1000 }, () => 128) })
    vi.mocked(filesApi.latestSplit).mockResolvedValue(null)
    vi.mocked(filesApi.split).mockReset()
  })
  afterEach(() => vi.useRealTimers())

  it("coupe à la position d'écoute et envoie les passages gardés, chacun vers son morceau", async () => {
    const wrapper = await mountPage()
    expect(wrapper.text()).toContain('Découper « repet.wav »')
    expect(wrapper.findAll('[data-testid="split-segment"]')).toHaveLength(1)

    await playTo(wrapper, 240_000)
    await wrapper.get('[data-testid="cut-here"]').trigger('click')
    await playTo(wrapper, 600_000)
    await wrapper.get('[data-testid="cut-here"]').trigger('click')
    const segments = wrapper.findAll('[data-testid="split-segment"]')
    expect(segments).toHaveLength(3)

    await segments[0]!.get('input[type="checkbox"]').setValue(false)          // l'accordage
    await segments[1]!.get('[data-testid="split-destination"]').setValue('song-n')
    await segments[2]!.get('[data-testid="split-title"]').setValue('Nouvelle idée')
    vi.mocked(filesApi.split).mockResolvedValue({
      id: 'sp1', status: 'Pending', error: null, requestedAt: '2026-10-24T20:00:00Z', completedAt: null, parts: [],
    })
    await wrapper.get('[data-testid="split-submit"]').trigger('click')
    await flushPromises()

    expect(filesApi.split).toHaveBeenCalledWith('s1', 'v1', 'f1', {
      parts: [
        { startMs: 240_000, endMs: 600_000, title: 'Nocturne', songId: 'song-n' },     // le nom suit le morceau choisi
        { startMs: 600_000, endMs: HOUR, title: 'Nouvelle idée' },
      ],
    })
    expect(wrapper.get('[data-testid="split-result"]').text()).toContain('en cours')
  })

  it('retire une coupure, et refuse un envoi sans passage gardé', async () => {
    const wrapper = await mountPage()
    await playTo(wrapper, 300_000)
    await wrapper.get('[data-testid="cut-here"]').trigger('click')

    await wrapper.get('[data-testid="split-merge"]').trigger('click')
    expect(wrapper.findAll('[data-testid="split-segment"]')).toHaveLength(1)

    await wrapper.get('input[type="checkbox"]').setValue(false)
    expect(wrapper.get('[data-testid="split-submit"]').attributes('disabled')).toBeDefined()
    expect(wrapper.text()).toContain('Gardez au moins un passage.')
  })

  it('suit un découpage en cours puis montre les versions créées', async () => {
    vi.useFakeTimers()
    const done: AudioSplit = {
      id: 'sp1', status: 'Done', error: null, requestedAt: '2026-10-24T20:00:00Z', completedAt: '2026-10-24T20:01:00Z',
      parts: [{ startMs: 0, endMs: 240_000, title: 'Intro', songId: null, createdSongId: 'new-1', createdVersionId: 'nv-1' }],
    }
    vi.mocked(filesApi.latestSplit).mockResolvedValueOnce({ ...done, status: 'Pending', completedAt: null }).mockResolvedValue(done)
    const wrapper = await mountPage()
    expect(wrapper.text()).toContain('Découpage en cours')

    await vi.advanceTimersByTimeAsync(3000)
    await flushPromises()

    expect(wrapper.findAllComponents(RouterLinkStub).map((l) => l.props('to'))).toContainEqual(
      { name: 'version-detail', params: { songId: 'new-1', versionId: 'nv-1' } })
  })

  it("montre la raison d'un échec et laisse recommencer", async () => {
    vi.mocked(filesApi.latestSplit).mockResolvedValue({
      id: 'sp1', status: 'Failed', error: 'Fichier abîmé.', requestedAt: '2026-10-24T20:00:00Z', completedAt: null, parts: [],
    })
    const wrapper = await mountPage()

    expect(wrapper.get('[data-testid="split-failed"]').text()).toContain('Fichier abîmé.')
    expect(wrapper.find('[data-testid="split-submit"]').exists()).toBe(true)
  })

  it('en lecture seule, ni coupure ni envoi', async () => {
    role.canWrite = false
    const wrapper = await mountPage()

    expect(wrapper.find('[data-testid="cut-here"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="split-submit"]').exists()).toBe(false)
  })
})
