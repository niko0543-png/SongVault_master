import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { bandsApi } from '../api/bandsApi'
import type { Band, BandRole } from '../types'

const STORAGE_KEY = 'songvault.band'

/** Ce qu'une action exige, même règle que l'API (ADR 0008) : write = Member, admin = Owner. */
export type BandAction = 'write' | 'admin'
const RANK: Record<BandRole, number> = { Owner: 3, Member: 2, Guest: 1 }
const REQUIRED: Record<BandAction, number> = { write: RANK.Member, admin: RANK.Owner }

export const useBandStore = defineStore('band', () => {
  const bands = ref<Band[]>([])
  const activeId = ref<string | null>(null)
  const isLoaded = ref(false)
  const active = computed(() => bands.value.find((b) => b.id === activeId.value) ?? null)
  let pending: Promise<void> | null = null

  /** Interroge /bands une seule fois (au premier passage dans la garde). */
  function ensureLoaded(): Promise<void> {
    if (isLoaded.value) return Promise.resolve()
    pending ??= bandsApi.mine()
      .then((list) => { bands.value = list; isLoaded.value = true })
      .finally(() => { pending = null })
    return pending
  }

  function has(id: string) { return bands.value.some((b) => b.id === id) }

  /** Groupe de la route courante, mémorisé pour la prochaine visite. */
  function setActive(id: string) {
    activeId.value = id
    try { localStorage.setItem(STORAGE_KEY, id) } catch { /* navigation privée */ }
  }

  /** Groupe à ouvrir sur « / » : le dernier utilisé s'il existe encore, sinon le premier. */
  function preferredId(): string | null {
    let saved: string | null = null
    try { saved = localStorage.getItem(STORAGE_KEY) } catch { /* idem */ }
    return saved && has(saved) ? saved : (bands.value[0]?.id ?? null)
  }

  async function create(name: string) {
    const band = await bandsApi.create(name)
    bands.value = [...bands.value, band].sort((a, b) => a.name.localeCompare(b.name))
    return band
  }

  /** Vrai si le rôle dans le groupe actif permet l'action. Sert à masquer des boutons : l'API reste juge (403). */
  function can(action: BandAction): boolean {
    const role = active.value?.role
    return role !== undefined && RANK[role] >= REQUIRED[action]
  }

  /** Après un changement de son propre rôle (page Membres). */
  function setRole(id: string, role: BandRole) {
    bands.value = bands.value.map((b) => (b.id === id ? { ...b, role } : b))
  }

  /** Après « Quitter le groupe ». Sans plus aucun groupe, la liste sera relue : l'API recrée le groupe personnel. */
  function remove(id: string) {
    bands.value = bands.value.filter((b) => b.id !== id)
    if (activeId.value === id) activeId.value = null
    if (bands.value.length === 0) isLoaded.value = false
  }

  function reset() { bands.value = []; activeId.value = null; isLoaded.value = false }

  return { bands, activeId, active, isLoaded, ensureLoaded, has, setActive, preferredId, create, can, setRole, remove, reset }
})