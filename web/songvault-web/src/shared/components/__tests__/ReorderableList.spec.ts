import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { nextTick } from 'vue'
import ReorderableList from '../ReorderableList.vue'

interface Item { id: string; name: string }
const items: Item[] = [{ id: 'a', name: 'Aube' }, { id: 'b', name: 'Brume' }, { id: 'c', name: 'Crépuscule' }]

function mountList(props: { disabled?: boolean } = {}) {
  const wrapper = mount(ReorderableList<Item>, {
    attachTo: document.body,
    props: {
      items, label: 'Pistes', itemKey: (i: Item) => i.id, itemLabel: (i: Item) => i.name, ...props,
      // Comme un parent qui enregistre : la liste reçue devient la nouvelle liste affichée
      onMove: (next: Item[]) => wrapper.setProps({ items: next }),
    },
    slots: { default: '<template #default="{ item }">{{ item.name }}</template>' },
  })
  return wrapper
}

const names = (w: ReturnType<typeof mount>) => w.findAll('[data-testid="reorderable-item"]').map((li) => li.text().replace(/[↑↓⠿]/g, '').trim())
const moves = (w: ReturnType<typeof mount>) => w.emitted<[Item[], number, number]>('move') ?? []

describe('ReorderableList', () => {
  it('grise Monter sur la première ligne et Descendre sur la dernière', () => {
    const w = mountList()

    expect(w.get('[aria-label="Monter Aube"]').attributes('disabled')).toBeDefined()
    expect(w.get('[aria-label="Descendre Aube"]').attributes('disabled')).toBeUndefined()
    expect(w.get('[aria-label="Descendre Crépuscule"]').attributes('disabled')).toBeDefined()
    w.unmount()
  })

  it('déplace au clavier, annonce la nouvelle place et garde le focus sur la ligne déplacée', async () => {
    const w = mountList()

    await w.get('[aria-label="Descendre Aube"]').trigger('click')
    await nextTick()

    const [next, from, to] = moves(w)[0]!
    expect(next.map((i) => i.id)).toEqual(['b', 'a', 'c'])
    expect([from, to]).toEqual([0, 1])
    expect(w.get('[role="status"]').text()).toBe('Aube déplacé en position 2 sur 3.')
    expect(document.activeElement?.getAttribute('aria-label')).toBe('Descendre Aube')
    w.unmount()
  })

  it("passe le focus à l'autre bouton quand la ligne arrive en bout de liste", async () => {
    const w = mountList()

    await w.get('[aria-label="Monter Brume"]').trigger('click')
    await nextTick()

    expect(names(w)).toEqual(['Brume', 'Aube', 'Crépuscule'])
    expect(document.activeElement?.getAttribute('aria-label')).toBe('Descendre Brume')   // Monter est grisé
    w.unmount()
  })

  it('déplace par glisser-déposer', async () => {
    const w = mountList()
    const rows = w.findAll('[data-testid="reorderable-item"]')
    const dataTransfer = { setData: () => undefined, effectAllowed: '' }

    await rows[0]!.trigger('dragstart', { dataTransfer })
    await rows[2]!.trigger('dragover')
    await rows[2]!.trigger('drop')

    expect(moves(w)[0]![0].map((i) => i.id)).toEqual(['b', 'c', 'a'])
    w.unmount()
  })

  it('sans droit de modification : ni boutons ni glisser-déposer', async () => {
    const w = mountList({ disabled: true })
    const rows = w.findAll('[data-testid="reorderable-item"]')

    expect(w.find('[data-move]').exists()).toBe(false)
    expect(rows[0]!.attributes('draggable')).toBe('false')
    await rows[0]!.trigger('dragstart', { dataTransfer: { setData: () => undefined } })
    await rows[1]!.trigger('drop')
    expect(moves(w)).toHaveLength(0)
    w.unmount()
  })
})