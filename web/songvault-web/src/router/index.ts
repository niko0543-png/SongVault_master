import { nextTick } from 'vue'
import { createRouter, createWebHistory } from 'vue-router'
import SongListPage from '@/features/songs/pages/SongListPage.vue'
import { useAuthStore } from '@/features/auth/stores/authStore'
import { useBandStore } from '@/features/bands/stores/bandStore'
import { resolveAuthNavigation } from './authGuard'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    // Accueil : la garde redirige vers /b/<groupe préféré>/songs
    { path: '/', name: 'home', component: SongListPage },
    // Anciens favoris (/songs, /songs/…) d'avant les groupes
    { path: '/songs/:rest(.*)*', redirect: '/' },

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

    // ---- Connexion exigée (par défaut), dans un groupe ----
    {
      path: '/b/:bandId/songs',
      name: 'songs',
      component: SongListPage,
      meta: { title: 'Morceaux' },
    },
    {
      path: '/b/:bandId/songs/new',
      name: 'song-create',
      component: () => import('@/features/songs/pages/SongCreatePage.vue'),
      meta: { title: 'Nouveau morceau' , write: true },
    },
    {
      path: '/b/:bandId/songs/:songId/edit',
      name: 'song-edit',
      component: () => import('@/features/songs/pages/SongEditPage.vue'),
      props: true,
      meta: { title: 'Modifier le morceau' , write: true },
    },
    {
      path: '/b/:bandId/songs/:songId/compare',
      name: 'version-compare',
      component: () => import('@/features/versions/pages/VersionComparePage.vue'),
      props: true,
      meta: { title: 'Comparer des versions' },
    },
    {
      path: '/b/:bandId/songs/:songId/versions/:versionId',
      name: 'version-detail',
      component: () => import('@/features/versions/pages/VersionDetailPage.vue'),
      props: true,
      meta: { title: 'Version' },
    },
    {
      path: '/b/:bandId/songs/:songId',
      name: 'song-detail',
      component: () => import('@/features/songs/pages/SongDetailPage.vue'),
      props: true,
      meta: { title: 'Détail du morceau' },
    },
    {
      path: '/b/:bandId/members',
      name: 'band-members',
      component: () => import('@/features/bands/pages/MembersPage.vue'),
      meta: { title: 'Membres' },
    },

    {
      path: '/:pathMatch(.*)*',
      name: 'not-found',
      component: () => import('@/shared/pages/NotFoundPage.vue'),
      meta: { public: true, title: 'Page introuvable' },
    },
  ],
})

// 1. Avant la navigation : contrôle d'accès, puis groupe actif
router.beforeEach(async (to) => {
  const auth = useAuthStore()
  await auth.ensureLoaded()
  const decision = resolveAuthNavigation(to, auth.isAuthenticated)
  if (decision !== true || to.meta.public) return decision

  const bands = useBandStore()
  await bands.ensureLoaded()
  const bandId = typeof to.params.bandId === 'string' ? to.params.bandId : null

  if (!bandId) {
    // « / » : ouvre le dernier groupe utilisé (ou le premier)
    const preferred = bands.preferredId()
    return preferred ? { name: 'songs', params: { bandId: preferred } } : true
  }
  // Groupe inconnu ou dont on n'est pas membre : même page que pour une adresse inexistante
  if (!bands.has(bandId)) return { name: 'not-found', params: { pathMatch: to.path.slice(1).split('/') } }
 bands.setActive(bandId)
  // Page d'écriture ouverte par un Guest (lien partagé, favori) : retour à la liste
  if (to.meta.write && !bands.can('write')) return { name: 'songs', params: { bandId } }
  return true
})

// 2. Après la navigation : titre de l'onglet + focus
router.afterEach((to, from, failure) => {
  if (failure) return                                   // navigation annulée (ex. « Quitter sans enregistrer ? » → Annuler)
  const band = to.params.bandId ? useBandStore().active?.name : undefined
  document.title = [to.meta.title, band, 'SongVault'].filter(Boolean).join(' · ')

  // Premier chargement, ou simple changement de query (recherche) : on ne déplace pas le focus
  if (!from.matched.length || to.path === from.path) return
  void nextTick(() => document.getElementById('main-content')?.focus({ preventScroll: true }))
})

export default router