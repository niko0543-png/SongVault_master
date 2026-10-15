import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import ShareLinksPanel from '../components/ShareLinksPanel.vue'
import { sharingApi } from '../api/sharingApi'
import type { ShareLink } from '../types'

vi.mock('../api/sharingApi')

const active: ShareLink = {
  id: 'l1', targetType: 'Version', targetId: 'v1', label: 'Studio Delta', hasPassword: true, status: 'Active',
  createdAt: '2026-10-14T09:00:00Z', expiresAt: '2026-11-13T09:00:00Z', revokedAt: null,
  createdByEmail: 'ana@test.local', openCount: 2, lastOpenedAt: '2026-10-14T10:00:00Z',
}
const revoked: ShareLink = { ...active, id: 'l2', label: null, hasPassword: false, status: 'Revoked',
  revokedAt: '2026-10-14T11:00:00Z', openCount: 0, lastOpenedAt: null }

async function mountPanel() {
  const wrapper = mount(ShareLinksPanel, { props: { target: { type: 'Version', id: 'v1' } } })
  await flushPromises()
  return wrapper
}

describe('ShareLinksPanel', () => {
  beforeEach(() => {
    vi.mocked(sharingApi.list).mockReset().mockResolvedValue([active, revoked])
    vi.mocked(sharingApi.create).mockReset().mockResolvedValue({ link: active, url: 'http://localhost:8080/s/abc' })
    vi.mocked(sharingApi.revoke).mockReset().mockResolvedValue(undefined)
    vi.mocked(sharingApi.accesses).mockReset().mockResolvedValue([
      { at: '2026-10-14T10:00:00Z', kind: 'Played', fileName: 'aube-mix.mp3', visitor: '3f2a9c1d', userAgent: 'Firefox' },
    ])
  })

  it('liste les liens de la cible avec leur état', async () => {
    const w = await mountPanel()

    expect(sharingApi.list).toHaveBeenCalledWith({ type: 'Version', id: 'v1' })
    const items = w.findAll('[data-testid="share-link-item"]')
    expect(items[0]?.text()).toContain('Studio Delta')
    expect(items[0]?.text()).toContain('avec mot de passe')
    expect(items[0]?.text()).toContain('2 ouvertures')
    expect(items[1]?.text()).toContain('Sans libellé')
    expect(items[1]?.text()).toContain('Révoqué le')
    // Un lien révoqué ne se révoque plus, il se supprime
    expect(items[1]?.findAll('button').map((b) => b.text())).toEqual(['Journal', 'Supprimer'])
  })

  it('crée un lien et le montre une seule fois', async () => {
    const w = await mountPanel()

    await w.get('#share-label').setValue('  Label X  ')
    await w.get('#share-days').setValue('7')
    await w.get('form').trigger('submit')
    await flushPromises()

    expect(sharingApi.create).toHaveBeenCalledWith(
      { type: 'Version', id: 'v1' }, { label: 'Label X', expiresInDays: 7, password: null })
    expect((w.get('[data-testid="share-link-url"] input').element as HTMLInputElement).value).toBe('http://localhost:8080/s/abc')
    expect(w.text()).toContain("Le mot de passe n'est pas dans le lien")
  })

  it('refuse un mot de passe trop court avant l\'envoi', async () => {
    const w = await mountPanel()

    await w.get('#share-password').setValue('12345')

    expect(w.text()).toContain('Au moins 6 caractères.')
    expect(w.get('button[type="submit"]').attributes('disabled')).toBeDefined()
  })

  it('révoque puis relit la liste', async () => {
    const w = await mountPanel()

    await w.findAll('[data-testid="share-link-item"]')[0]!.findAll('button')
      .find((b) => b.text() === 'Révoquer')!.trigger('click')
    await flushPromises()

    expect(sharingApi.revoke).toHaveBeenCalledWith('l1')
    expect(sharingApi.list).toHaveBeenCalledTimes(2)
  })

  it('ouvre le journal du lien', async () => {
    const w = await mountPanel()

    await w.findAll('[data-testid="share-link-item"]')[0]!.get('button').trigger('click')
    await flushPromises()

    expect(sharingApi.accesses).toHaveBeenCalledWith('l1')
    expect(w.get('[data-testid="share-journal"]').text()).toContain('Écoute · aube-mix.mp3')
    expect(w.get('[data-testid="share-journal"]').text()).toContain('3f2a9c1d')
  })
})