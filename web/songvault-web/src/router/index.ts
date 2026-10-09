import { nextTick } from 'vue'
import { createRouter, createWebHistory } from 'vue-router'
import SongListPage from '@/features/songs/pages/SongListPage.vue'
import { useAuthStore } from '@/features/auth/stores/authStore'
import { resolveAuthNavigation } from './authGuard'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: '/', redirect: { name: 'songs' } },   // pas de meta : une redirection n'affiche aucune page

    // ---- Publiques ----
    {
      path: '/login',
      name: 'login',
      component: () => import('@/features/auth/pages/LoginPage.vue'),
      meta: { public: true, guestOnly: true, title: 'Connexion' },
    },
    {
      path: '/register',
      name: 'register',
      component: () => import('@/features/auth/pages/RegisterPage.vue'),
      meta: { public: true, guestOnly: true, title: 'Créer un compte' },
    },

    // ---- Connexion exigée (par défaut) ----
    {
      path: '/songs',
      name: 'songs',
      component: SongListPage,
      meta: { title: 'Morceaux' },
    },
    {
      path: '/songs/new',
      name: 'song-create',
      component: () => import('@/features/songs/pages/SongCreatePage.vue'),
      meta: { title: 'Nouveau morceau' },
    },
    {
      path: '/songs/:songId/edit',
      name: 'song-edit',
      component: () => import('@/features/songs/pages/SongEditPage.vue'),
      props: true,
      meta: { title: 'Modifier le morceau' },
    },
    {
      path: '/songs/:songId/compare',
      name: 'version-compare',
      component: () => import('@/features/versions/pages/VersionComparePage.vue'),
      props: true,
      meta: { title: 'Comparer des versions' },
    },
    {
      path: '/songs/:songId/versions/:versionId',
      name: 'version-detail',
      component: () => import('@/features/versions/pages/VersionDetailPage.vue'),
      props: true,
      meta: { title: 'Version' },
    },
    {
      path: '/songs/:songId',
      name: 'song-detail',
      component: () => import('@/features/songs/pages/SongDetailPage.vue'),
      props: true,
      meta: { title: 'Détail du morceau' },
    },

    {
      path: '/:pathMatch(.*)*',
      name: 'not-found',
      component: () => import('@/shared/pages/NotFoundPage.vue'),
      meta: { public: true, title: 'Page introuvable' },
    },
  ],
})

// 1. Avant la navigation : contrôle d'accès (inchangé)
router.beforeEach(async (to) => {
  const auth = useAuthStore()
  await auth.ensureLoaded()
  return resolveAuthNavigation(to, auth.isAuthenticated)
})

// 2. Après la navigation : titre de l'onglet + focus
router.afterEach((to, from, failure) => {
  if (failure) return                                   // navigation annulée (ex. « Quitter sans enregistrer ? » → Annuler)
  document.title = to.meta.title ? `${to.meta.title} · SongVault` : 'SongVault'

  // Premier chargement, ou simple changement de query (recherche) : on ne déplace pas le focus
  if (!from.matched.length || to.path === from.path) return
  void nextTick(() => document.getElementById('main-content')?.focus({ preventScroll: true }))
})

export default router