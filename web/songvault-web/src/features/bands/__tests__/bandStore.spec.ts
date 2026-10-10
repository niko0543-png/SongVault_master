import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useBandStore } from '../stores/bandStore'
import { bandsApi } from '../api/bandsApi'
import type { Band } from '../types'

vi.mock('../api/bandsApi')     // aucun appel réseau

const perso: Band = { id: 'b1', name: 'Groupe de nico', role: 'Owner' }
const autre: Band = { id: 'b2', name: 'Les Autres', role: 'Owner' }

describe('bandStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    vi.mocked(bandsApi.mine).mockReset().mockResolvedValue([perso, autre])
  })

  it('rend le groupe mémorisé quand il existe encore', async () => {
    const store = useBandStore()
    await store.ensureLoaded()
    store.setActive('b2')
    expect(store.preferredId()).toBe('b2')
  })

  it('rend le premier groupe quand le groupe mémorisé a disparu', async () => {
    localStorage.setItem('songvault.band', 'supprime')
    const store = useBandStore()
    await store.ensureLoaded()
    expect(store.preferredId()).toBe('b1')
  })

  it("n'appelle l'API qu'une fois, même en appels simultanés", async () => {
    const store = useBandStore()
    await Promise.all([store.ensureLoaded(), store.ensureLoaded()])
    expect(bandsApi.mine).toHaveBeenCalledTimes(1)
  })

  it('reset vide tout', async () => {
    const store = useBandStore()
    await store.ensureLoaded()
    store.setActive('b1')
    store.reset()
    expect(store.bands).toEqual([])
    expect(store.activeId).toBeNull()
    expect(store.isLoaded).toBe(false)
  })
  it.each([
    ['Owner', true, true],
    ['Member', true, false],
    ['Guest', false, false],
  ] as const)('can() pour %s : write=%s, admin=%s', async (role, write, admin) => {
    vi.mocked(bandsApi.mine).mockResolvedValue([{ ...perso, role }])
    const store = useBandStore()
    await store.ensureLoaded()
    store.setActive('b1')

    expect(store.can('write')).toBe(write)
    expect(store.can('admin')).toBe(admin)
  })

  it('can() refuse tout sans groupe actif', () => {
    expect(useBandStore().can('write')).toBe(false)
  })

  it("remove retire le groupe et force une relecture quand il n'en reste aucun", async () => {
    vi.mocked(bandsApi.mine).mockResolvedValue([perso])
    const store = useBandStore()
    await store.ensureLoaded()
    store.setActive('b1')

    store.remove('b1')

    expect(store.bands).toEqual([])
    expect(store.activeId).toBeNull()
    expect(store.isLoaded).toBe(false)
  })
})