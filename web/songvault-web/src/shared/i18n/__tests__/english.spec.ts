import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { setLocale, t } from '..'
import NotFoundPage from '@/shared/pages/NotFoundPage.vue'
import PrivacyPage from '@/features/legal/pages/PrivacyPage.vue'
import { statusLabels } from '@/features/versions/types'
import { describe as describeAudit } from '@/features/audit/describe'
import type { AuditItem } from '@/features/audit/types'
import { authApi } from '@/features/auth/api/authApi'
import { useAuthStore } from '@/features/auth/stores/authStore'
import type { CurrentUser } from '@/features/auth/types'
import { request } from '@/shared/api/httpClient'

afterEach(() => {
  setLocale('fr')
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})

function router() {
  const Stub = { render: () => null }
  return createRouter({
    history: createMemoryHistory(),
    routes: [{ path: '/', name: 'home', component: Stub }, { path: '/account', name: 'account', component: Stub }],
  })
}

describe('interface en anglais (#26)', () => {
  it('une page passe en anglais, puis revient en français', async () => {
    setLocale('en')
    const w = mount(NotFoundPage, { global: { plugins: [router()] } })
    expect(w.text()).toContain('Page not found')
    expect(w.text()).toContain('Back to the songs')
    setLocale('fr')
    await flushPromises()
    expect(w.text()).toContain('Page introuvable')
  })

  it('pages légales : en anglais seulement, une ligne dit que le français fait foi', async () => {
    const w = mount(PrivacyPage, { global: { plugins: [router()] } })
    expect(w.find('[data-testid="french-prevails"]').exists()).toBe(false)
    setLocale('en')
    await flushPromises()
    expect(w.get('[data-testid="french-prevails"]').text()).toContain('the French version of this text prevails')
    expect(w.get('h1').text()).toBe('Privacy policy')
  })

  it('pluriels : la règle de chaque langue (0 et 1 au singulier en français)', () => {
    expect(t('songs.list.count', { count: 0 })).toBe('0 morceau')
    expect(t('songs.list.count', { count: 1 })).toBe('1 morceau')
    expect(t('songs.list.count', { count: 2 })).toBe('2 morceaux')
    setLocale('en')
    expect(t('songs.list.count', { count: 0 })).toBe('0 songs')
    expect(t('songs.list.count', { count: 1 })).toBe('1 song')
    expect(t('songs.list.count', { count: 1200 })).toBe('1,200 songs')
  })

  it('tables de libellés et phrases recomposées suivent la langue', () => {
    expect(statusLabels.Rehearsal).toBe('Répétition')
    const item = { action: 'MemberRemoved', actorId: 'me', actorEmail: 'moi@test.local', targetUserId: 'u2',
      targetEmail: 'bob@test.local', subject: null, details: null } as unknown as AuditItem
    expect(describeAudit(item, 'me')).toBe('Vous avez retiré bob@test.local du groupe')
    setLocale('en')
    expect(statusLabels.Rehearsal).toBe('Rehearsal')
    expect(describeAudit(item, 'me')).toBe('You removed bob@test.local from the band')
    expect(describeAudit({ ...item, actorId: 'u3', actorEmail: 'ana@test.local' }, 'me'))
      .toBe('ana@test.local removed bob@test.local from the band')
  })
})

describe('langue du compte (#26)', () => {
  it('connecté : le choix est enregistré sur le compte', async () => {
    setActivePinia(createPinia())
    const auth = useAuthStore()
    auth.user = { id: 'u1', email: 'moi@test.local', locale: 'fr' }
    const save = vi.spyOn(authApi, 'saveLocale').mockResolvedValue(undefined)

    await auth.changeLocale('en')

    expect(save).toHaveBeenCalledWith('en')
    expect(auth.user?.locale).toBe('en')
    expect(t('common.actions.cancel')).toBe('Cancel')
  })

  it('anonyme : seulement sur cet appareil', async () => {
    setActivePinia(createPinia())
    const save = vi.spyOn(authApi, 'saveLocale')
    await useAuthStore().changeLocale('en')
    expect(save).not.toHaveBeenCalled()
    expect(localStorage.getItem('songvault.locale')).toBe('en')
  })

  it('à la connexion, la langue du compte l’emporte sur celle de l’appareil', async () => {
    setActivePinia(createPinia())
    const me: CurrentUser = { id: 'u1', email: 'moi@test.local', locale: 'en' }
    vi.spyOn(authApi, 'me').mockResolvedValue(me)
    await useAuthStore().ensureLoaded()
    expect(t('common.actions.close')).toBe('Close')
  })
})

describe('langue envoyée à l’API (#26)', () => {
  it('chaque requête porte Accept-Language, qui suit la langue choisie', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockImplementation(async () => new Response('{}', { headers: { 'Content-Type': 'application/json' } }))
    vi.stubGlobal('fetch', fetchMock)

    await request('/status')
    setLocale('en')
    await request('/status')

    const sent = fetchMock.mock.calls.map(([, init]) => (init?.headers as Record<string, string>)['Accept-Language'])
    expect(sent).toEqual(['fr', 'en'])
  })
})
