import { describe, expect, it, vi } from 'vitest'
import { registerServiceWorker } from '../registerServiceWorker'

function fakeWindow() {
  const listeners: Record<string, () => void> = {}
  return {
    listeners,
    addEventListener: vi.fn((type: string, listener: () => void) => { listeners[type] = listener }),
  }
}

describe('registerServiceWorker', () => {
  it('installe /sw.js une fois la page chargée, en production', () => {
    const register = vi.fn(() => Promise.resolve({} as ServiceWorkerRegistration))
    const win = fakeWindow()

    registerServiceWorker({ register } as unknown as ServiceWorkerContainer, true, win as unknown as Window)
    expect(register).not.toHaveBeenCalled()             // pas avant « load » : le premier affichage passe d'abord
    win.listeners.load!()

    expect(register).toHaveBeenCalledWith('/sw.js', { scope: '/' })
  })

  it('ne fait rien en développement ni sans service worker', () => {
    const register = vi.fn()
    const win = fakeWindow()

    registerServiceWorker({ register } as unknown as ServiceWorkerContainer, false, win as unknown as Window)
    registerServiceWorker(undefined, true, win as unknown as Window)

    expect(win.addEventListener).not.toHaveBeenCalled()
  })

  it("un refus du navigateur n'empêche pas l'application de marcher", async () => {
    const register = vi.fn(() => Promise.reject(new Error('SecurityError')))
    const win = fakeWindow()
    registerServiceWorker({ register } as unknown as ServiceWorkerContainer, true, win as unknown as Window)

    expect(() => win.listeners.load!()).not.toThrow()
    await Promise.resolve()                               // le rejet est absorbé, pas d'erreur non gérée
  })
})