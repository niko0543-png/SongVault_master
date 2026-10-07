import { createRouter, createWebHistory } from 'vue-router'
import SongListPage from '@/features/songs/pages/SongListPage.vue'
import { useAuthStore } from '@/features/auth/stores/authStore'
import { resolveAuthNavigation } from './authGuard'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: '/', redirect: { name: 'songs' } },

    // ---- Publiques ----
    {
      path: '/login',
      name: 'login',
      component: () => import('@/features/auth/pages/LoginPage.vue'),
      meta: { public: true, guestOnly: true },
    },
    {
      path: '/register',
      name: 'register',
      component: () => import('@/features/auth/pages/RegisterPage.vue'),
      meta: { public: true, guestOnly: true },
    },

    // ---- Connexion exigée (par défaut) ----
    { path: '/songs', name: 'songs', component: SongListPage },
    { path: '/songs/new', name: 'song-create', component: () => import('@/features/songs/pages/SongCreatePage.vue') },
    {
      path: '/songs/:songId/edit',
      name: 'song-edit',
      component: () => import('@/features/songs/pages/SongEditPage.vue'),
      props: true,
    },
    {
      path: '/songs/:songId/compare',
      name: 'version-compare',
      component: () => import('@/features/versions/pages/VersionComparePage.vue'),
      props: true,
    },
    {
      path: '/songs/:songId/versions/:versionId',
      name: 'version-detail',
      component: () => import('@/features/versions/pages/VersionDetailPage.vue'),
      props: true,
    },
    {
      path: '/songs/:songId',
      name: 'song-detail',
      component: () => import('@/features/songs/pages/SongDetailPage.vue'),
      props: true,
    },

    {
      path: '/:pathMatch(.*)*',
      name: 'not-found',
      component: () => import('@/shared/pages/NotFoundPage.vue'),
      meta: { public: true },
    },
  ],
})

router.beforeEach(async (to) => {
  const auth = useAuthStore()
  await auth.ensureLoaded()                       // /api/auth/me, une seule fois
  return resolveAuthNavigation(to, auth.isAuthenticated)
})

export default router