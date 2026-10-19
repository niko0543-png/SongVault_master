import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import SongRightsSection from '../components/SongRightsSection.vue'
import { rightsApi } from '../api/rightsApi'
import type { SongRights } from '../types'
import { ApiError } from '@/shared/api/ApiError'

// Les adresses CSV restent les vraies fonctions ; seuls les appels réseau sont simulés
vi.mock('../api/rightsApi', async (importOriginal) => {
  const real = await importOriginal<typeof import('../api/rightsApi')>()
  return { rightsApi: { ...real.rightsApi, get: vi.fn(), save: vi.fn(), setIsrc: vi.fn() } }
})

const rights: SongRights = {
  songId: 's1', songTitle: 'Nocturne', iswc: 'T-034.524.680-1',
  credits: [
    { name: 'Alice', role: 'Author', sharePercent: 50 },
    { name: 'Bruno', role: 'Composer', sharePercent: 50 },
  ],
  totalPercent: 100,
  versions: [{ id: 'v1', number: 1, title: 'Mix final', isrc: null }],
}

async function mountSection(readonly = false) {
  const wrapper = mount(SongRightsSection, {
    props: { songId: 's1', readonly, versionCount: 1 },
    global: {
      plugins: [createTestingPinia({
        createSpy: vi.fn,
        initialState: { band: { bands: [{ id: 'b1', name: 'Les Rats', role: readonly ? 'Guest' : 'Member' }], activeId: 'b1', isLoaded: true } },
      })],
    },
  })
  await flushPromises()
  return wrapper
}

const shareInputs = (w: Awaited<ReturnType<typeof mountSection>>) => w.findAll('input[inputmode="decimal"]')
const saveButton = (w: Awaited<ReturnType<typeof mountSection>>) =>
  w.findAll('button[type="submit"]').find((b) => b.text() === 'Enregistrer les crédits')!

describe('SongRightsSection', () => {
  beforeEach(() => {
    vi.mocked(rightsApi.get).mockReset().mockResolvedValue(structuredClone(rights))
    vi.mocked(rightsApi.save).mockReset().mockImplementation(async (_id, input) => ({
      ...structuredClone(rights), iswc: input.iswc, credits: input.credits,
      totalPercent: input.credits.reduce((s, c) => s + c.sharePercent, 0),
    }))
    vi.mocked(rightsApi.setIsrc).mockReset()
  })

  it('affiche le total en direct et refuse d’enregistrer à 99 %', async () => {
    const wrapper = await mountSection()
    expect(wrapper.get('[data-testid="credits-total"]').text()).toBe('Total : 100 %')

    await shareInputs(wrapper)[1]!.setValue('49')

    expect(wrapper.get('[data-testid="credits-total"]').text()).toBe('Total : 99 %')
    expect(wrapper.get('[data-testid="credits-error"]').text()).toBe('Le total fait 99 % : il doit faire exactement 100 %.')
    expect(saveButton(wrapper).attributes('disabled')).toBeDefined()
    await wrapper.get('form').trigger('submit')
    expect(rightsApi.save).not.toHaveBeenCalled()
  })

  it('enregistre les parts saisies à la française, en nombres', async () => {
    const wrapper = await mountSection()
    await wrapper.findAll('button').find((b) => b.text() === 'Ajouter un ayant droit')!.trigger('click')
    await wrapper.findAll('button').find((b) => b.text() === 'Répartir à parts égales')!.trigger('click')
    const rows = wrapper.findAll('[data-testid="credit-row"]')
    await rows[2]!.get('input').setValue('Éditions du Nord')
    await rows[2]!.get('select').setValue('Publisher')

    expect(shareInputs(wrapper).map((i) => (i.element as HTMLInputElement).value)).toEqual(['33,33', '33,33', '33,34'])
    expect(saveButton(wrapper).attributes('disabled')).toBeUndefined()
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(rightsApi.save).toHaveBeenCalledWith('s1', {
      iswc: 'T-034.524.680-1',
      credits: [
        { name: 'Alice', role: 'Author', sharePercent: 33.33 },
        { name: 'Bruno', role: 'Composer', sharePercent: 33.33 },
        { name: 'Éditions du Nord', role: 'Publisher', sharePercent: 33.34 },
      ],
    })
  })

  it('montre le refus du serveur, par exemple un ISWC dont la clé est fausse', async () => {
    vi.mocked(rightsApi.save).mockRejectedValue(new ApiError(422, {
      title: 'ISWC invalide : « T-034.524.680-2 ». La clé de contrôle ne correspond pas (vérifiez les chiffres).', status: 422,
    }))
    const wrapper = await mountSection()
    await wrapper.get('#song-iswc').setValue('T-034.524.680-2')
    await wrapper.get('form').trigger('submit')
    await flushPromises()

    expect(wrapper.get('[role="alert"]').text()).toContain('La clé de contrôle ne correspond pas')
  })

  it('enregistre l’ISRC d’une version et affiche sa forme lisible', async () => {
    vi.mocked(rightsApi.setIsrc).mockResolvedValue({ id: 'v1', number: 1, title: 'Mix final', isrc: 'FR-Z03-26-00001' })
    const wrapper = await mountSection()
    const line = wrapper.get('[data-testid="version-isrc"]')
    await line.get('input').setValue('frz032600001')
    await line.get('form').trigger('submit')
    await flushPromises()

    expect(rightsApi.setIsrc).toHaveBeenCalledWith('s1', 'v1', 'frz032600001')
    expect((wrapper.get('[data-testid="version-isrc"] input').element as HTMLInputElement).value).toBe('FR-Z03-26-00001')
  })

  it('un Guest lit les crédits et l’export, sans champ à modifier', async () => {
    const wrapper = await mountSection(true)

    expect(wrapper.find('input').exists()).toBe(false)
    expect(wrapper.text()).toContain('T-034.524.680-1')
    expect(wrapper.findAll('tbody tr').map((r) => r.text())).toEqual(['AliceAuteur50 %', 'BrunoCompositeur50 %'])
    expect(wrapper.get('a[download]').attributes('href')).toBe('/api/bands/b1/songs/s1/rights.csv')
  })
})