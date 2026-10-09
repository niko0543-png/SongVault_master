import type { RouteLocationNormalized, RouteLocationRaw } from 'vue-router'

// Champs meta propres à SongVault, typés pour tout le routeur
declare module 'vue-router' {
  interface RouteMeta {
    public?: boolean
    guestOnly?: boolean
    /** Titre affiché dans l'onglet du navigateur. */
    title?: string
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