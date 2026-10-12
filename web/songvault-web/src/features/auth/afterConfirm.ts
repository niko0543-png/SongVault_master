import { safeRedirect } from '@/router/authGuard'

// Page à rouvrir après la confirmation de l'adresse (ex. une invitation, #3).
// Gardée dans le navigateur : le lien de l'e-mail s'ouvre souvent dans un nouvel onglet.
const STORAGE_KEY = 'songvault.afterConfirm'

export function rememberAfterConfirm(redirect: unknown) {
  const target = safeRedirect(redirect)
  try {
    if (target === '/') localStorage.removeItem(STORAGE_KEY)
    else localStorage.setItem(STORAGE_KEY, target)
  } catch { /* navigation privée */ }
}

/** Page à rouvrir, sans l'oublier. */
export function peekAfterConfirm(): string {
  try { return safeRedirect(localStorage.getItem(STORAGE_KEY)) } catch { return '/' }
}

/** Page à rouvrir, oubliée aussitôt (elle ne sert qu'une fois). */
export function takeAfterConfirm(): string {
  const target = peekAfterConfirm()
  try { localStorage.removeItem(STORAGE_KEY) } catch { /* idem */ }
  return target
}

/** Query de la page de connexion : retour vers cette page s'il y en a une, adresse préremplie. */
export function loginQuery(target: string, email?: string) {
  return { ...(target === '/' ? {} : { redirect: target }), ...(email ? { email } : {}) }
}