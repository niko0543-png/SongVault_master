import { describe, expect, it } from 'vitest'
import { isGlobalShortcut } from '../keyboard'

const event = (target: EventTarget | null, mods: Partial<Record<'ctrlKey' | 'metaKey' | 'altKey', boolean>> = {}) =>
  ({ target, ctrlKey: false, metaKey: false, altKey: false, ...mods })

describe('isGlobalShortcut', () => {
  it('accepte une touche sur le fond de la page', () => {
    expect(isGlobalShortcut(event(document.body))).toBe(true)
  })

  it.each(['input', 'textarea', 'select', 'button', 'a'])('ignore une touche dans un élément %s', (tag) => {
    expect(isGlobalShortcut(event(document.createElement(tag)))).toBe(false)
  })

  it('ignore une combinaison avec Ctrl', () => {
    expect(isGlobalShortcut(event(document.body, { ctrlKey: true }))).toBe(false)
  })
})