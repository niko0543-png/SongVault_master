import { describe, expect, it } from 'vitest'
import type { RouteMeta } from 'vue-router'
import { resolveAuthNavigation, safeRedirect } from '../authGuard'

const route = (meta: RouteMeta = {}, fullPath = '/songs/123') => ({ meta, fullPath })

describe('resolveAuthNavigation', () => {
  it('redirige un anonyme vers /login en mémorisant la page demandée', () => {
    expect(resolveAuthNavigation(route({}), false))
      .toEqual({ name: 'login', query: { redirect: '/songs/123' } })
  })

  it('laisse passer un utilisateur connecté', () => {
    expect(resolveAuthNavigation(route({}), true)).toBe(true)
  })

  it("laisse un anonyme accéder à une page publique (pas de boucle sur /login)", () => {
    expect(resolveAuthNavigation(route({ public: true, guestOnly: true }, '/login'), false)).toBe(true)
  })

  it('renvoie un utilisateur connecté de /login vers la liste', () => {
    expect(resolveAuthNavigation(route({ public: true, guestOnly: true }, '/login'), true)).toEqual({ name: 'songs' })
  })
})

describe('safeRedirect', () => {
  it.each([
    ['/songs/123', '/songs/123'],
    ['//site-malveillant.com', '/songs'],
    ['https://site-malveillant.com', '/songs'],
    ['/\\site-malveillant.com', '/songs'],
    [undefined, '/songs'],
    [['/songs/1'], '/songs'],
  ])('%j → %s', (input, expected) => {
    expect(safeRedirect(input)).toBe(expected)
  })
})
