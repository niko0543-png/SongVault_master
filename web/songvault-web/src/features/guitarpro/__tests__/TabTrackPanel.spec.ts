import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import TabTrackPanel from '../components/TabTrackPanel.vue'

/** #31 : le panneau des pistes façon Guitar Pro (ADR 0037). */
const bar = (rest: boolean) => ({ isRestOnly: rest })
const tracks = [
  { name: 'Guitare', color: { rgba: 'rgb(255, 0, 0)' }, staves: [{ bars: [bar(false), bar(true), bar(false)] }] },
  { name: 'Basse', color: { rgba: 'rgb(0, 0, 255)' }, staves: [{ bars: [bar(true), bar(false), bar(false)] }] },
]

function mountPanel(props: Record<string, unknown> = {}) {
  return mount(TabTrackPanel, {
    props: { tracks: tracks as never, shown: [0, 1], mute: [false, false], solo: [false, false], volume: [1, 1],
             currentBar: 2, barCount: 3, disabled: false, ...props },
  })
}

describe('TabTrackPanel (#31)', () => {
  it('montre où chaque piste joue, à sa couleur, et la mesure en cours', () => {
    const wrapper = mountPanel()
    const lanes = wrapper.findAll('.lane').map((lane) => lane.findAll('.cell').map((c) => c.attributes('style') ?? ''))

    expect(lanes[0]!.map((style) => style.includes('rgb(255, 0, 0)'))).toEqual([true, false, true])
    expect(lanes[1]!.map((style) => style.includes('rgb(0, 0, 255)'))).toEqual([false, true, true])
    expect(wrapper.get('.now').attributes('style')).toContain('left: 16px')
    expect(wrapper.get('output').text()).toBe('Mesure 2 sur 3')
  })

  it('émet muet, solo, affichage, volume et mesure choisie, et estompe les pistes qu’on n’entend pas', async () => {
    const wrapper = mountPanel({ solo: [true, false] })

    await wrapper.get('[aria-label="Couper Basse"]').trigger('click')
    await wrapper.get('[aria-label="Écouter Guitare seule"]').trigger('click')
    await wrapper.get('[aria-label="Afficher Basse"]').trigger('change')
    await wrapper.get('[aria-label="Volume de Guitare"]').setValue('1.5')
    await wrapper.findAll('.lane')[0]!.findAll('.cell')[2]!.trigger('click')

    expect(wrapper.emitted()).toMatchObject({ toggleMute: [[1]], toggleSolo: [[0]], toggleShown: [[1]], volume: [[0, 1.5]], goToBar: [[2]] })
    expect(wrapper.findAll('.row').map((row) => row.classes('silent'))).toEqual([false, true])
  })

  it('se replie, et bloque muet, solo et volume quand la tablature suit une maquette', async () => {
    const wrapper = mountPanel({ disabled: true })

    expect(wrapper.get('[aria-label="Couper Guitare"]').attributes('disabled')).toBeDefined()
    expect(wrapper.get('[aria-label="Volume de Guitare"]').attributes('disabled')).toBeDefined()
    await wrapper.get('[data-testid="tab-tracks-toggle"]').trigger('click')
    expect(wrapper.find('.body').exists()).toBe(false)
    expect(wrapper.get('[data-testid="tab-tracks-toggle"]').attributes('aria-expanded')).toBe('false')
  })
})
