import { createRouter, createWebHistory } from 'vue-router'
import SongListPage from '@/features/songs/pages/SongListPage.vue'

export default createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    { path: '/', redirect: { name: 'songs' } },
    { path: '/songs', name: 'songs', component: SongListPage },
    {
      path: '/songs/:songId',
      name: 'song-detail',
      component: () => import('@/features/songs/pages/SongDetailPage.vue'),
      props: true,
    },
    { path: '/:pathMatch(.*)*', name: 'not-found', component: () => import('@/shared/pages/NotFoundPage.vue') },
    { path: '/songs/new', name: 'song-create', component: () => import('@/features/songs/pages/SongCreatePage.vue') },
    {
      path: '/songs/:songId/edit',
      name: 'song-edit',
      component: () => import('@/features/songs/pages/SongEditPage.vue'),
      props: true,
    },
    {
      path: '/songs/:songId/versions/:versionId',
      name: 'version-detail',
      component: () => import('@/features/versions/pages/VersionDetailPage.vue'),
      props: true,
    },
    {
      path: '/songs/:songId/compare',
      name: 'version-compare',
      component: () => import('@/features/versions/pages/VersionComparePage.vue'),
      props: true,
    },
  ],
})
