// Service worker de SongVault (#14, ADR 0020).
// Rôle : rendre l'application installable et afficher une page claire hors connexion.
// Il ne garde JAMAIS de données : /api/ (morceaux, fichiers audio, session) passe toujours par le réseau.
// Changer VERSION à chaque modification de ce fichier : l'ancien cache est effacé à l'activation.
const VERSION = 'v1'
const SHELL = `songvault-shell-${VERSION}`     // page hors connexion et icônes
const ASSETS = `songvault-assets-${VERSION}`   // JS et CSS de Vite (nom avec empreinte, donc jamais périmés)
const MAX_ASSETS = 60                           // les fichiers des anciens déploiements finissent par sortir
const OFFLINE_URL = '/offline.html'

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(SHELL)
      .then((cache) => cache.addAll([OFFLINE_URL, '/icons/icon-192.png', '/icons/icon-512.png']))
      .then(() => self.skipWaiting()),          // pas de cache de pages : la nouvelle version peut prendre la main tout de suite
  )
})

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(keys.filter((k) => k !== SHELL && k !== ASSETS).map((k) => caches.delete(k))))
      .then(() => self.clients.claim()),
  )
})

self.addEventListener('fetch', (event) => {
  const request = event.request
  const url = new URL(request.url)
  // Autre origine (stockage Azure), autre méthode que GET, ou API : le navigateur fait comme sans service worker
  if (request.method !== 'GET' || url.origin !== self.location.origin || url.pathname.startsWith('/api/')) return

  // Pages : toujours le réseau (index.html n'est jamais servi périmé), la page hors connexion s'il ne répond pas
  if (request.mode === 'navigate') {
    event.respondWith(fetch(request).catch(() => caches.match(OFFLINE_URL)))
    return
  }

  // JS et CSS de Vite : le cache d'abord, ils ne changent jamais sous le même nom
  if (url.pathname.startsWith('/assets/')) {
    event.respondWith(fromCacheThenNetwork(request))
  }
})

async function fromCacheThenNetwork(request) {
  const cache = await caches.open(ASSETS)
  const cached = await cache.match(request)
  if (cached) return cached
  const response = await fetch(request)
  if (response.ok) {
    await cache.put(request, response.clone())
    const keys = await cache.keys()
    for (const old of keys.slice(0, Math.max(0, keys.length - MAX_ASSETS))) await cache.delete(old)
  }
  return response
}