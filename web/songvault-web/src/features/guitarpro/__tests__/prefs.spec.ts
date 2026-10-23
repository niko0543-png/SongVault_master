import { afterEach, describe, expect, it, vi } from 'vitest'
import { DEFAULT_VIEW, loadFilePrefs, loadViewPrefs, saveFilePrefs, saveViewPrefs } from '../prefs'

afterEach(() => { vi.restoreAllMocks(); localStorage.clear() })

describe('Réglages du lecteur gardés sur l’appareil (#33)', () => {
  it('relit ce qui a été gardé, réglage par réglage', () => {
    saveViewPrefs({ zoom: 1.5 })
    saveViewPrefs({ panelOpen: false })
    expect(loadViewPrefs()).toEqual({ zoom: 1.5, staffView: 'file', panelOpen: false, panelHeight: null, settingsOpen: false })

    saveFilePrefs('f', { speed: 0.5, mute: [false, true], solo: [false, false], volume: [1, 0.3] })
    expect(loadFilePrefs('f', 2, { min: 0.25, max: 1.5 })).toEqual({ speed: 0.5, mute: [false, true], solo: [false, false], volume: [1, 0.3] })
    expect(loadFilePrefs('f', 3, { min: 0.25, max: 1.5 })).toEqual({ speed: 0.5 })    // le fichier a changé de pistes
  })

  it('sans stockage (navigation privée), les réglages par défaut, sans erreur', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('SecurityError') })
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => { throw new Error('QuotaExceededError') })
    expect(() => saveViewPrefs({ zoom: 1.5 })).not.toThrow()
    expect(loadViewPrefs()).toEqual(DEFAULT_VIEW)
    expect(loadFilePrefs('f', 1, { min: 0.25, max: 1.5 })).toEqual({})
  })
})
