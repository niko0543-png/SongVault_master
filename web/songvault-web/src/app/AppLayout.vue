<script setup lang="ts">
import { watch } from 'vue'
import { RouterLink, RouterView, useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'
import AudioPlayer from '@/features/player/components/AudioPlayer.vue'
import { useAuthStore } from '@/features/auth/stores/authStore'
import { useBandStore } from '@/features/bands/stores/bandStore'
import BandSwitcher from '@/features/bands/components/BandSwitcher.vue'
import { useSongsStore } from '@/features/songs/stores/songsStore'
import { usePlayerStore } from '@/features/player/stores/playerStore'
import ToastHost from '@/shared/components/ToastHost.vue'          // S8
import ConfirmDialog from '@/shared/components/ConfirmDialog.vue'  // S8

const auth = useAuthStore()
const { user, isAuthenticated } = storeToRefs(auth)
const bands = useBandStore()
const router = useRouter()

// Changement de groupe : la liste en cache et la lecture en cours appartiennent à l'ancien groupe
watch(() => bands.activeId, (next, prev) => {
  if (prev && next !== prev) { useSongsStore().reset(); usePlayerStore().stop() }
})

async function onLogout() {
  await auth.logout()
  await router.push({ name: 'login' })
}
</script>

<template>
  <!-- S8 : premier élément de la page, donc premier atteint avec Tab -->
  <a href="#main-content" class="skip-link">Aller au contenu</a>

 <header class="app-header">
    <RouterLink to="/" class="brand">🎸 SongVault</RouterLink>
    <div v-if="isAuthenticated" class="session">
      <BandSwitcher v-if="bands.isLoaded" />
      <span class="muted">{{ user?.email }}</span>
      <button type="button" class="secondary" @click="onLogout">Se déconnecter</button>
    </div>
  </header>

  <!-- S8 : id = cible du lien et du focus après navigation ; tabindex="-1" = focalisable par le code seulement -->
  <main id="main-content" class="app-main" tabindex="-1">
    <RouterView />
  </main>

  <AudioPlayer v-if="isAuthenticated" />

  <!-- S8 : montés une seule fois pour toute l'application -->
  <ToastHost />
  <ConfirmDialog />
</template>

<style scoped>
.app-header { display: flex; justify-content: space-between; align-items: center; gap: 1rem; flex-wrap: wrap;
              padding: 1rem 1.5rem; background: var(--surface); border-bottom: 1px solid var(--border); }
.brand { font-weight: 700; text-decoration: none; font-size: 1.2rem; }
.session { display: flex; align-items: center; gap: .75rem; }
.app-main { max-width: 900px; margin: 0 auto; padding: 1.5rem 1.5rem 7rem; }

/* S8 : lien d'évitement, caché au-dessus de l'écran tant qu'il n'a pas le focus */
.skip-link { position: absolute; left: .5rem; top: -3rem; padding: .5rem 1rem;
             background: var(--primary); color: #fff; border-radius: var(--radius);
             z-index: 100; text-decoration: none; }
.skip-link:focus { top: .5rem; }

/* S8 : pas de cadre autour de toute la page quand le focus y est placé par le code */
.app-main:focus { outline: none; }
</style>