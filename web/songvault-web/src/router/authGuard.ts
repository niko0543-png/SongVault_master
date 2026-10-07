import type { RouteLocationNormalized, RouteLocationRaw } from 'vue-router'

// Champs meta propres à SongVault, typés pour tout le routeur
declare module 'vue-router' {
  interface RouteMeta {
    /** Accessible sans connexion (sinon : connexion exigée, par défaut). */
    public?: boolean
    /** Réservé aux visiteurs NON connectés (connexion, inscription). */
    guestOnly?: boolean
  }
}

/** Décide de la navigation : true pour passer, ou une destination de redirection. */
export function resolveAuthNavigation(
  to: Pick<RouteLocationNormalized, 'meta' | 'fullPath'>,
  isAuthenticated: boolean,
): true | RouteLocationRaw {
  if (to.meta.public) {
    return to.meta.guestOnly && isAuthenticated ? { name: 'songs' } : true
  }
  return isAuthenticated ? true : { name: 'login', query: { redirect: to.fullPath } }
}

/** N'accepte qu'un chemin interne : bloque les redirections ouvertes (//site.com, https://…). */
export function safeRedirect(value: unknown): string {
  return typeof value === 'string' && value.startsWith('/') && !value.startsWith('//') && !value.startsWith('/\\')
    ? value
    : '/songs'
}