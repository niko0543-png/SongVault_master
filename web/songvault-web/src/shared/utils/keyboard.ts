const INTERACTIVE_TAGS = new Set(['INPUT', 'TEXTAREA', 'SELECT', 'BUTTON', 'A'])

/** Vrai si une touche peut servir de raccourci global (l'utilisateur n'écrit pas, pas de combinaison). */
export function isGlobalShortcut(event: Pick<KeyboardEvent, 'target' | 'ctrlKey' | 'metaKey' | 'altKey'>): boolean {
  if (event.ctrlKey || event.metaKey || event.altKey) return false
  const target = event.target
  if (target instanceof HTMLElement) {
    if (target.isContentEditable) return false
    if (INTERACTIVE_TAGS.has(target.tagName)) return false
  }
  return true
}