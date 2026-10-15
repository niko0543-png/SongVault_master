/**
 * #14 : installe le service worker (public/sw.js) une fois la page chargée, pour ne pas ralentir le premier affichage.
 * Seulement en production : en développement, Vite sert des fichiers sans empreinte qu'il ne faut pas garder en cache.
 */
export function registerServiceWorker(
  container: ServiceWorkerContainer | undefined = globalThis.navigator?.serviceWorker,
  isProduction: boolean = import.meta.env.PROD,
  target: Pick<Window, 'addEventListener'> = window,
) {
  if (!container || !isProduction) return
  target.addEventListener('load', () => {
    container.register('/sw.js', { scope: '/' }).catch(() => {
      // Navigation privée de Firefox, HTTP hors localhost… : l'application marche aussi sans
    })
  })
}