import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { createMemoryHistory, createRouter } from 'vue-router'
import RecordPage from '../pages/RecordPage.vue'
import { songsApi } from '@/features/songs/api/songsApi'
import { versionsApi } from '@/features/versions/api/versionsApi'
import { filesApi } from '@/features/files/api/filesApi'
import type { Song } from '@/features/songs/types'
import type { SongVersion } from '@/features/versions/types'
import type { SongFile } from '@/features/files/types'

vi.mock('@/features/songs/api/songsApi', () => ({ songsApi: { list: vi.fn(), create: vi.fn() } }))
vi.mock('@/features/versions/api/versionsApi', () => ({ versionsApi: { list: vi.fn(), create: vi.fn() } }))
vi.mock('@/features/files/api/filesApi', () => ({ filesApi: { upload: vi.fn() } }))

/** MediaRecorder de test : rend un bloc de son à l'arrêt, comme le vrai. */
class FakeMediaRecorder {
  static isTypeSupported = (type: string) => type.startsWith('audio/webm')
  state: 'inactive' | 'recording' | 'paused' = 'inactive'
  mimeType: string
  ondataavailable: ((event: { data: Blob }) => void) | null = null
  onstop: (() => void) | null = null
  onerror: (() => void) | null = null
  constructor(_stream: MediaStream, options: { mimeType: string }) { this.mimeType = options.mimeType }
  start() { this.state = 'recording' }
  pause() { this.state = 'paused' }
  resume() { this.state = 'recording' }
  stop() {
    this.state = 'inactive'
    this.ondataavailable?.({ data: new Blob(['son'], { type: 'audio/webm' }) })
    this.onstop?.()
  }
}

const track = { stop: vi.fn(), addEventListener: vi.fn() }
const stream = { getTracks: () => [track], getAudioTracks: () => [track] } as unknown as MediaStream

async function mountPage(query = '') {
  const Stub = { render: () => null }
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/b/:bandId/record', name: 'record', component: RecordPage },
      { path: '/b/:bandId/songs/:songId/versions/:versionId', name: 'version-detail', component: Stub },
      { path: '/b/:bandId/billing', name: 'billing', component: Stub },
    ],
  })
  await router.push(`/b/b1/record${query}`)
  const wrapper = mount(RecordPage, {
    global: {
      plugins: [router, createTestingPinia({
        createSpy: vi.fn,
        stubActions: false,
        initialState: { band: { bands: [{ id: 'b1', name: 'Les Rats', role: 'Member' }], activeId: 'b1', isLoaded: true } },
      })],
    },
  })
  await flushPromises()
  return { wrapper, router }
}

async function recordSomething(wrapper: Awaited<ReturnType<typeof mountPage>>['wrapper']) {
  await wrapper.get('[data-testid="recording-start"]').trigger('click')
  await flushPromises()
  await wrapper.get('[data-testid="recording-stop"]').trigger('click')
  await flushPromises()
}

describe('RecordPage', () => {
  const getUserMedia = vi.fn()

  beforeEach(() => {
    vi.stubGlobal('MediaRecorder', FakeMediaRecorder)
    getUserMedia.mockReset().mockResolvedValue(stream)
    Object.defineProperty(navigator, 'mediaDevices', {
      configurable: true,
      value: { getUserMedia, enumerateDevices: vi.fn().mockResolvedValue([]) },
    })
    URL.createObjectURL = vi.fn(() => 'blob:enregistrement')
    URL.revokeObjectURL = vi.fn()
    vi.mocked(songsApi.list).mockReset().mockResolvedValue({
      items: [{ id: 's1', title: 'Fièvre' } as Song], page: 1, pageSize: 50, totalCount: 1, totalPages: 1,
    })
    vi.mocked(versionsApi.list).mockReset().mockResolvedValue([
      { id: 'v1', number: 1, title: 'Maquette', status: 'Demo', createdAt: '2026-10-01T00:00:00Z' },
    ])
    vi.mocked(songsApi.create).mockReset()
    vi.mocked(versionsApi.create).mockReset()
    vi.mocked(filesApi.upload).mockReset().mockResolvedValue({ id: 'f1' } as SongFile)
  })

  afterEach(() => vi.unstubAllGlobals())

  it('prévient quand le navigateur ne sait pas enregistrer', async () => {
    vi.stubGlobal('MediaRecorder', undefined)
    const { wrapper } = await mountPage()
    expect(wrapper.find('[data-testid="recording-unsupported"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="recording-start"]').exists()).toBe(false)
  })

  it('demande le micro sans les traitements de la voix', async () => {
    const { wrapper } = await mountPage()
    await wrapper.get('[data-testid="recording-start"]').trigger('click')
    await flushPromises()
    expect(getUserMedia).toHaveBeenCalledWith({ audio: expect.objectContaining({ echoCancellation: false, autoGainControl: false }) })
    expect(wrapper.find('[data-testid="recording-pause"]').exists()).toBe(true)
  })

  it('accès refusé : message clair, on peut réessayer', async () => {
    getUserMedia.mockRejectedValue(new DOMException('refus', 'NotAllowedError'))
    const { wrapper } = await mountPage()
    await wrapper.get('[data-testid="recording-start"]').trigger('click')
    await flushPromises()
    expect(wrapper.get('[data-testid="recording-error"]').text()).toContain('Accès au micro refusé')
    expect(wrapper.find('[data-testid="recording-start"]').exists()).toBe(true)
  })

  it('par défaut : crée un morceau et sa version « Répétition », envoie le fichier, ouvre la version', async () => {
    vi.mocked(songsApi.create).mockResolvedValue({ id: 's2', title: 'Répétition' } as Song)
    vi.mocked(versionsApi.create).mockResolvedValue({ id: 'v9', number: 1, title: 'Répétition', status: 'Rehearsal', createdAt: '' } as SongVersion)
    const { wrapper, router } = await mountPage()
    await recordSomething(wrapper)

    expect(wrapper.get('[data-testid="recording-summary"]').text()).toMatch(/^repetition-\d{4}-\d{2}-\d{2}-\d{4}\.webm/)
    await wrapper.get('#recording-title').setValue('Répét du jeudi')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(songsApi.create).toHaveBeenCalledWith({ title: 'Répét du jeudi', artist: null, description: null })
    expect(versionsApi.create).toHaveBeenCalledWith('s2', expect.objectContaining({ title: 'Répét du jeudi', status: 'Rehearsal' }))
    const [songId, versionId, file] = vi.mocked(filesApi.upload).mock.calls[0]!
    expect([songId, versionId]).toEqual(['s2', 'v9'])
    expect(file.name).toMatch(/\.webm$/)
    expect(router.currentRoute.value.fullPath).toBe('/b/b1/songs/s2/versions/v9')
  })

  it('lien depuis une version : envoie dans cette version sans rien créer', async () => {
    const { wrapper, router } = await mountPage('?songId=s1&versionId=v1')
    await recordSomething(wrapper)

    expect((wrapper.get('#recording-version').element as HTMLSelectElement).value).toBe('v1')
    expect(wrapper.find('#recording-title').exists()).toBe(false)
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(songsApi.create).not.toHaveBeenCalled()
    expect(versionsApi.create).not.toHaveBeenCalled()
    expect(vi.mocked(filesApi.upload).mock.calls[0]!.slice(0, 2)).toEqual(['s1', 'v1'])
    expect(router.currentRoute.value.fullPath).toBe('/b/b1/songs/s1/versions/v1')
  })

  it("envoi en échec : le morceau créé n'est pas recréé au nouvel essai", async () => {
    vi.mocked(songsApi.create).mockResolvedValue({ id: 's2', title: 'Répétition' } as Song)
    vi.mocked(versionsApi.create).mockResolvedValue({ id: 'v9', number: 1, title: 'Répétition', status: 'Rehearsal', createdAt: '' } as SongVersion)
    vi.mocked(filesApi.upload).mockRejectedValueOnce(new Error('réseau'))
    const { wrapper } = await mountPage()
    await recordSomething(wrapper)

    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(wrapper.find('[data-testid="recording-send-error"]').exists()).toBe(true)

    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(songsApi.create).toHaveBeenCalledTimes(1)
    expect(versionsApi.create).toHaveBeenCalledTimes(1)
    expect(filesApi.upload).toHaveBeenCalledTimes(2)
  })
})
