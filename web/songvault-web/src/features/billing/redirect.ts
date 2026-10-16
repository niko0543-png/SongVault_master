/** Quitte SongVault pour une page Stripe (paiement, portail). Isolé pour être remplacé dans les tests. */
export function redirectTo(url: string) {
  window.location.assign(url)
}