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

  it('règle le volume général sur la piste « Général », même quand la tablature suit une maquette', async () => {
    const wrapper = mountPanel({ disabled: true, master: 0.8 })
    const master = wrapper.get('[aria-label="Volume général"]')

    expect(master.attributes('disabled')).toBeUndefined()
    expect(master.attributes('aria-valuetext')).toBe('80 %')
    expect(wrapper.get('[data-testid="tab-master"]').text()).toContain('Général')
    await master.setValue('0.35')
    expect(wrapper.emitted('update:master')).toEqual([[0.35]])
  })

  it('#34 : avec une seule piste, garde le volume et la vue d’ensemble, sans case ni muet/solo ; montre le temps', () => {
    const wrapper = mountPanel({ tracks: [tracks[0]] as never, shown: [0], mute: [false], solo: [false], volume: [1], time: '0:42 / 2:31' })
    expect(wrapper.find('input[type="checkbox"]').exists()).toBe(false)
    expect(wrapper.findAll('.chip')).toHaveLength(0)
    expect(wrapper.find('[aria-label="Volume de Guitare"]').exists()).toBe(true)
    expect(wrapper.findAll('.lane')).toHaveLength(1)
    expect(wrapper.get('output').text()).toBe('Mesure 2 sur 3 · 0:42 / 2:31')
  })

  it('#35 : se redimensionne en glissant la poignée ou au clavier, entre une piste et les trois quarts de l’écran', async () => {
    const wrapper = mountPanel({ height: 200 })
    const grip = wrapper.get('[data-testid="tab-tracks-resize"]')
    const body = wrapper.get('[data-testid="tab-tracks-body"]')
    expect(body.attributes('style')).toContain('height: 200px')
    body.element.getBoundingClientRect = () => ({ height: 200 }) as DOMRect

    // jsdom n'a pas PointerEvent : un MouseEvent du même nom porte clientY
    const pointer = (type: string, clientY = 0) => grip.element.dispatchEvent(new MouseEvent(type, { clientY, bubbles: true, cancelable: true }))
    pointer('pointerdown', 500)
    pointer('pointermove', 400)                                              // vers le haut : plus grand
    pointer('pointerup')
    expect(wrapper.emitted('update:height')!.slice(-1)[0]).toEqual([300])

    await wrapper.setProps({ height: 300 })
    await grip.trigger('keydown', { key: 'ArrowDown' })
    expect(wrapper.emitted('update:height')!.slice(-1)[0]).toEqual([276])
    await grip.trigger('keydown', { key: 'End' })
    expect(wrapper.emitted('update:height')!.slice(-1)[0]).toEqual([48])
    await grip.trigger('keydown', { key: 'Home' })
    expect(wrapper.emitted('update:height')!.slice(-1)[0]).toEqual([Math.round(window.innerHeight * 0.75)])
    await grip.trigger('dblclick')                                            // double-clic : hauteur par défaut
    expect(wrapper.emitted('update:height')!.slice(-1)[0]).toEqual([null])
  })
})
