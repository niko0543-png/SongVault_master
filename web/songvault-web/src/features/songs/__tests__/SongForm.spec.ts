import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import SongForm from '../components/SongForm.vue'

describe('SongForm', () => {
  it("refuse un titre vide et n'émet pas submit", async () => {
    const wrapper = mount(SongForm)

    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')).toBeUndefined()
    expect(wrapper.find('[data-testid="title-error"]').text()).toContain('obligatoire')
  })

  it('émet des valeurs nettoyées (espaces retirés, vide → null)', async () => {
    const wrapper = mount(SongForm)

    await wrapper.find('[data-testid="title-input"]').setValue('  Nocturne  ')
    await wrapper.find('[data-testid="artist-input"]').setValue('   ')
    await wrapper.find('form').trigger('submit')

    expect(wrapper.emitted('submit')?.[0]).toEqual([{ title: 'Nocturne', artist: null, description: null }])
  })

  it('affiche une erreur renvoyée par le serveur', () => {
    const wrapper = mount(SongForm, { props: { serverErrors: { title: 'Titre refusé par le serveur.' } } })

    expect(wrapper.find('[data-testid="title-error"]').text()).toBe('Titre refusé par le serveur.')
  })

  it('préremplit les champs en édition', () => {
    const wrapper = mount(SongForm, { props: { initial: { title: 'Lumière', artist: 'Band', description: null } } })

    expect((wrapper.find('[data-testid="title-input"]').element as HTMLInputElement).value).toBe('Lumière')
  })
})
