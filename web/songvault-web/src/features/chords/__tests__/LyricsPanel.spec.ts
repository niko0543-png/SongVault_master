import { describe, expect, it } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import LyricsPanel from '../components/LyricsPanel.vue'
import { useConfirm } from '@/shared/composables/useConfirm'

const LYRICS = '[Am]Je marche [F]seul\n[C]sous la [G]pluie'

function mountPanel(props: Partial<{ modelValue: string; musicalKey: string; canWrite: boolean; dirty: boolean }> = {}) {
  return mount(LyricsPanel, { props: { modelValue: LYRICS, musicalKey: 'Am', canWrite: true, dirty: false, ...props } })
}

const button = (w: ReturnType<typeof mountPanel>, text: string) => w.findAll('button').find((b) => b.text() === text)
const chords = (w: ReturnType<typeof mountPanel>) => w.findAll('.chord').map((c) => c.text()).filter(Boolean)

describe('LyricsPanel', () => {
  it('affiche les accords au-dessus des paroles', () => {
    const w = mountPanel()
    expect(chords(w)).toEqual(['Am', 'F', 'C', 'G'])
    expect(w.find('textarea').exists()).toBe(false)
  })

  it('transpose à l’affichage sans toucher au texte', async () => {
    const w = mountPanel()
    for (let i = 0; i < 3; i++) await button(w, '+½ ton')!.trigger('click')
    expect(chords(w)).toEqual(['Cm', 'Ab', 'Eb', 'Bb'])
    expect(w.get('output').text()).toBe('Am → Cm (+3)')
    expect(w.emitted('update:modelValue')).toBeUndefined()

    await button(w, 'Revenir à l’original')!.trigger('click')
    expect(chords(w)).toEqual(['Am', 'F', 'C', 'G'])
  })

  it('laisse un invité transposer, sans rien enregistrer', async () => {
    const w = mountPanel({ canWrite: false })
    await button(w, '−½ ton')!.trigger('click')
    expect(chords(w)).toEqual(['G#m', 'E', 'B', 'F#'])
    expect(button(w, 'Modifier le texte')).toBeUndefined()
    expect(button(w, 'Enregistrer dans cette tonalité')).toBeUndefined()
  })

  it('enregistre la transposition après confirmation, avec la tonalité', async () => {
    const w = mountPanel()
    await button(w, '+½ ton')!.trigger('click')
    await button(w, '+½ ton')!.trigger('click')
    await button(w, 'Enregistrer dans cette tonalité')!.trigger('click')
    expect(useConfirm().pending.value?.title).toBe('Enregistrer les accords en Bm ?')
    useConfirm().answer(true)
    await flushPromises()

    expect(w.emitted('save-transposed')).toEqual([['[Bm]Je marche [G]seul\n[D]sous la [A]pluie', 'Bm']])
    expect(w.get('output').text()).toBe('Am (d’origine)')
  })

  it('n’enregistre pas une transposition par-dessus des modifications en cours', async () => {
    const w = mountPanel({ dirty: true })
    await button(w, '+½ ton')!.trigger('click')
    expect(button(w, 'Enregistrer dans cette tonalité')!.attributes('disabled')).toBeDefined()
  })

  it('ouvre le texte à modifier quand il n’y a pas encore de paroles', async () => {
    const w = mountPanel({ modelValue: '' })
    await w.get('textarea').setValue('[D]Nouveau')
    expect(w.emitted('update:modelValue')).toEqual([['[D]Nouveau']])
    expect(w.text()).toContain('Accords entre crochets')
  })

  it('ne propose pas de transposer des paroles sans accords', () => {
    const w = mountPanel({ modelValue: 'La nuit tombe' })
    expect(button(w, '+½ ton')).toBeUndefined()
    expect(w.text()).toContain('La nuit tombe')
  })
})