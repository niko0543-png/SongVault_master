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
  ],
})
