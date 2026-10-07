<script setup lang="ts">
import { RouterLink, RouterView, useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'
import AudioPlayer from '@/features/player/components/AudioPlayer.vue'
import { useAuthStore } from '@/features/auth/stores/authStore'

const auth = useAuthStore()
const { user, isAuthenticated } = storeToRefs(auth)
const router = useRouter()

async function onLogout() {
  await auth.logout()
  await router.push({ name: 'login' })
}
</script>

<template>
  <header class="app-header">
    <RouterLink to="/songs" class="brand">🎸 SongVault</RouterLink>
    <div v-if="isAuthenticated" class="session">
      <span class="muted">{{ user?.email }}</span>
      <button type="button" class="secondary" @click="onLogout">Se déconnecter</button>
    </div>
  </header>
  <main class="app-main">
    <RouterView />
  </main>
  <AudioPlayer v-if="isAuthenticated" />
</template>

<style scoped>
.app-header { display: flex; justify-content: space-between; align-items: center; gap: 1rem; flex-wrap: wrap;
              padding: 1rem 1.5rem; background: var(--surface); border-bottom: 1px solid var(--border); }
.brand { font-weight: 700; text-decoration: none; font-size: 1.2rem; }
.session { display: flex; align-items: center; gap: .75rem; }
.app-main { max-width: 900px; margin: 0 auto; padding: 1.5rem 1.5rem 7rem; }
</style>