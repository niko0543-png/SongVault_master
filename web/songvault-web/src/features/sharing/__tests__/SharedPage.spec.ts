import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import SharedPage from '../pages/SharedPage.vue'
import { sharingApi } from '../api/sharingApi'
import type { SharedContent } from '../types'
import { ApiError } from '@/shared/api/ApiError'

// Remplace le module entier : aucun appel réseau pendant les tests
vi.mock('../api/sharingApi')

const release: SharedContent = {
  targetType: 'Release', bandName: 'Les Nocturnes', title: 'Nuits blanches', releaseType: 'EP', releaseDate: '2026-11-20',
  hasCover: true, expiresAt: '2026-11-13T09:00:00Z',
  tracks: [
    { number: 1, songTitle: 'Brume', artist: null, versionNumber: 2, versionTitle: 'Master', files: [
      { id: 'f1', fileName: 'brume-master.wav', contentType: 'audio/wav', sizeBytes: 1000 },
    ] },
    { number: 2, songTitle: 'Aube', artist: 'Ana', versionNumber: 3, versionTitle: 'Mix 2', files: [] },
  ],
}

const problem = (status: number) => new ApiError(status, { title: 'Erreur', status })

async function mountPage() {
  const wrapper = mount(SharedPage, { props: { token: 'tok' } })
  await flushPromises()
  return wrapper
}

describe('SharedPage', () => {
  beforeEach(() => {
    vi.mocked(sharingApi.open).mockReset().mockResolvedValue(release)
    vi.mocked(sharingApi.unlock).mockReset().mockResolvedValue(undefined)
    vi.mocked(sharingApi.fileUrl).mockImplementation((token, id) => `/api/share/${token}/files/${id}/content`)
    vi.mocked(sharingApi.coverUrl).mockImplementation((token) => `/api/share/${token}/cover`)
  })

  it('affiche les pistes dans l\'ordre, avec un lecteur par fichier audio', async () => {
    const w = await mountPage()

    expect(w.get('h1').text()).toBe('Nuits blanches')
    expect(w.text()).toContain('Les Nocturnes vous fait écouter')
    const tracks = w.findAll('[data-testid="shared-track"]')
    expect(tracks.map((t) => t.get('h2').text())).toEqual(['1. Brume', '2. Aube · Ana'])
    const audio = w.get('audio')
    expect(audio.attributes('src')).toBe('/api/share/tok/files/f1/content')
    expect(audio.attributes('preload')).toBe('none')
    expect(tracks[1]?.text()).toContain('Pas encore de fichier audio.')
    expect(w.get('img.cover').attributes('src')).toBe('/api/share/tok/cover')
  })

  it('lien protégé : demande le mot de passe, puis affiche la sortie', async () => {
    vi.mocked(sharingApi.open).mockRejectedValueOnce(problem(403))
    const w = await mountPage()
    expect(w.get('h1').text()).toBe('Écoute protégée')

    await w.get('#share-password').setValue('écoute-v3')
    await w.get('form').trigger('submit')
    await flushPromises()

    expect(sharingApi.unlock).toHaveBeenCalledWith('tok', 'écoute-v3')
    expect(w.get('h1').text()).toBe('Nuits blanches')
  })

  it('mauvais mot de passe : le dit, sans recharger', async () => {
    vi.mocked(sharingApi.open).mockRejectedValue(problem(403))
    vi.mocked(sharingApi.unlock).mockRejectedValue(problem(422))
    const w = await mountPage()

    await w.get('#share-password').setValue('mauvais')
    await w.get('form').trigger('submit')
    await flushPromises()

    expect(w.get('[role="alert"]').text()).toBe('Mot de passe incorrect.')
    expect(sharingApi.open).toHaveBeenCalledTimes(1)
  })

  it('lien inconnu, expiré ou révoqué : le même message', async () => {
    vi.mocked(sharingApi.open).mockRejectedValue(problem(404))
    const w = await mountPage()

    expect(w.get('[role="alert"]').text()).toContain("n'existe pas, a expiré ou a été révoqué")
  })

  it('demande aux moteurs de recherche de ne pas indexer la page', async () => {
    document.head.querySelectorAll('meta[name="robots"]').forEach((m) => m.remove())   // laissées par les tests précédents
    const w = await mountPage()

    expect(document.head.querySelector('meta[name="robots"]')?.getAttribute('content')).toBe('noindex, nofollow')
    w.unmount()
    expect(document.head.querySelector('meta[name="robots"]')).toBeNull()
  })
})