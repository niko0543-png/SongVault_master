import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import TwoFactorSection from '../components/TwoFactorSection.vue'
import { accountApi } from '../api/accountApi'
import { ApiError } from '@/shared/api/ApiError'
import { useConfirm } from '@/shared/composables/useConfirm'

vi.mock('../api/accountApi')

const CODES = ['aaaa1-bbbb1', 'aaaa2-bbbb2']

async function mountSection() {
  const wrapper = mount(TwoFactorSection, { global: { plugins: [createTestingPinia({ createSpy: vi.fn })] } })
  await flushPromises()
  return wrapper
}

describe('TwoFactorSection', () => {
  beforeEach(() => {
    vi.mocked(accountApi.twoFactorStatus).mockReset()
      .mockResolvedValue({ enabled: false, recoveryCodesLeft: 0, browserRemembered: false })
    vi.mocked(accountApi.setupTwoFactor).mockReset()
      .mockResolvedValue({ sharedKey: 'JBSW Y3DP EHPK 3PXP', authenticatorUri: 'otpauth://totp/SongVault:moi?secret=JBSWY3DPEHPK3PXP' })
    vi.mocked(accountApi.enableTwoFactor).mockReset().mockResolvedValue(CODES)
    vi.mocked(accountApi.disableTwoFactor).mockReset().mockResolvedValue(undefined)
  })

  it('active : montre la clé, vérifie un code, puis montre les codes de secours une fois', async () => {
    const w = await mountSection()

    await w.get('button').trigger('click')                      // « Activer la double authentification »
    await flushPromises()
    expect(w.get('[data-testid="shared-key"]').text()).toBe('JBSW Y3DP EHPK 3PXP')
    expect(w.get('a').attributes('href')).toMatch(/^otpauth:\/\/totp\//)

    vi.mocked(accountApi.twoFactorStatus).mockResolvedValue({ enabled: true, recoveryCodesLeft: 2, browserRemembered: false })
    await w.get('#two-factor-code').setValue('123 456')
    await w.get('form').trigger('submit')
    await flushPromises()

    expect(accountApi.enableTwoFactor).toHaveBeenCalledWith('123456')
    expect(w.findAll('[data-testid="recovery-codes"] li').map((li) => li.text())).toEqual(CODES)
    expect(w.emitted('changed')).toHaveLength(1)

    await w.findAll('button').find((b) => b.text() === 'J’ai noté mes codes')!.trigger('click')
    expect(w.text()).toContain('2 codes de secours restants')
  })

  it('un code refusé reste sur la saisie avec le message de l’API', async () => {
    vi.mocked(accountApi.enableTwoFactor).mockRejectedValue(
      new ApiError(422, { title: 'Règle métier non respectée', status: 422, detail: 'Code incorrect : vérifiez l’heure.' }))
    const w = await mountSection()
    await w.get('button').trigger('click')
    await flushPromises()

    await w.get('#two-factor-code').setValue('000000')
    await w.get('form').trigger('submit')
    await flushPromises()

    expect(w.get('[role="alert"]').text()).toContain('Code incorrect')
    expect(w.find('[data-testid="shared-key"]').exists()).toBe(true)
  })

  it('désactiver demande le mot de passe puis une confirmation', async () => {
    vi.mocked(accountApi.twoFactorStatus).mockResolvedValue({ enabled: true, recoveryCodesLeft: 10, browserRemembered: false })
    const w = await mountSection()
    const disable = () => w.findAll('button').find((b) => b.text() === 'Désactiver')!.trigger('click')

    await disable()
    expect(w.get('[role="alert"]').text()).toBe('Saisissez votre mot de passe.')

    await w.get('#two-factor-password').setValue('Mot-de-passe1!')
    await disable()
    useConfirm().answer(true)
    await flushPromises()

    expect(accountApi.disableTwoFactor).toHaveBeenCalledWith('Mot-de-passe1!')
  })
})