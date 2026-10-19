<script setup lang="ts">
import { ref, watch } from 'vue'
import { RouterLink, RouterView, useRoute, useRouter } from 'vue-router'
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
const route = useRoute()

// #14 : sur un téléphone, la navigation est repliée derrière le bouton « Menu »
const isMenuOpen = ref(false)
watch(() => route.fullPath, () => { isMenuOpen.value = false })   // une page choisie referme le menu

// Changement de groupe : la liste en cache et la lecture en cours appartiennent à l'ancien groupe
watch(() => bands.activeId, (next, prev) => {
  if (prev && next !== prev) { useSongsStore().reset(); usePlayerStore().stop() }
})

async function onLogout() {
  isMenuOpen.value = false
  await auth.logout()
  await router.push({ name: 'login' })
}
</script>

<template>
  <!-- S8 : premier élément de la page, donc premier atteint avec Tab -->
  <a href="#main-content" class="skip-link">Aller au contenu</a>

  <header class="app-header">
    <RouterLink to="/" class="brand">🎸 SongVault</RouterLink>
    <!-- #14 : visible sous 720 px seulement (CSS) ; au-dessus, la navigation est toujours affichée -->
    <button v-if="isAuthenticated" type="button" class="secondary menu-toggle" aria-controls="app-nav"
            :aria-expanded="isMenuOpen" @click="isMenuOpen = !isMenuOpen">
      {{ isMenuOpen ? 'Fermer' : 'Menu' }}
    </button>
    <nav v-if="isAuthenticated" id="app-nav" class="session" :class="{ open: isMenuOpen }" aria-label="Navigation principale">
      <BandSwitcher v-if="bands.isLoaded" />
      <RouterLink v-if="bands.activeId" :to="{ name: 'releases', params: { bandId: bands.activeId } }">Sorties</RouterLink>
      <RouterLink v-if="bands.activeId" :to="{ name: 'activity', params: { bandId: bands.activeId } }">Activité</RouterLink>
      <RouterLink v-if="bands.activeId" :to="{ name: 'band-members', params: { bandId: bands.activeId } }">Membres</RouterLink>
      <RouterLink v-if="bands.activeId && bands.can('write')" :to="{ name: 'trash', params: { bandId: bands.activeId } }">Corbeille</RouterLink>
      <RouterLink v-if="bands.activeId && bands.can('admin')" :to="{ name: 'billing', params: { bandId: bands.activeId } }">Facturation</RouterLink>
      <RouterLink v-if="bands.activeId && bands.can('admin')" :to="{ name: 'audit', params: { bandId: bands.activeId } }">Journal</RouterLink>
      <RouterLink :to="{ name: 'account' }" class="muted account-link" title="Mon compte">{{ user?.email }}</RouterLink>
      <button type="button" class="secondary" @click="onLogout">Se déconnecter</button>
    </nav>
  </header>

  <!-- S8 : id = cible du lien et du focus après navigation ; tabindex="-1" = focalisable par le code seulement -->
  <main id="main-content" class="app-main" tabindex="-1">
    <RouterView />
  </main>

  <footer class="app-footer">
    <RouterLink :to="{ name: 'legal-notice' }">Mentions légales</RouterLink>
    <RouterLink :to="{ name: 'privacy' }">Confidentialité</RouterLink>
    <RouterLink :to="{ name: 'plans' }">Formules</RouterLink>
    <RouterLink :to="{ name: 'terms-of-sale' }">CGV</RouterLink>
    <RouterLink :to="{ name: 'status' }">État du service</RouterLink>
  </footer>

  <AudioPlayer v-if="isAuthenticated" />

  <!-- S8 : montés une seule fois pour toute l'application -->
  <ToastHost />
  <ConfirmDialog />
</template>

<style scoped>
.app-header { display: flex; justify-content: space-between; align-items: center; gap: 1rem; flex-wrap: wrap;
              padding: 1rem 1.5rem; background: var(--surface); border-bottom: 1px solid var(--border); }
.brand { font-weight: 700; text-decoration: none; font-size: 1.2rem; }
.session { display: flex; align-items: center; gap: .75rem; flex-wrap: wrap; min-width: 0; }
.account-link { overflow-wrap: anywhere; }
.menu-toggle { display: none; }
.app-main { max-width: 900px; margin: 0 auto; padding: 1.5rem 1.5rem 2rem; }
/* #5 : la marge basse laisse la place au lecteur audio, fixé en bas de l'écran (#14 : et à la barre d'accueil de l'iPhone) */
.app-footer { display: flex; justify-content: center; gap: 1.5rem; flex-wrap: wrap;
              padding: 1rem 1.5rem calc(7rem + env(safe-area-inset-bottom)); border-top: 1px solid var(--border); font-size: .9rem; }

/* #14 : téléphone. Le menu s'ouvre sous l'en-tête, une entrée par ligne, chacune assez haute pour le pouce */
@media (max-width: 720px) {
  .app-header { padding: .75rem 1rem; }
  .menu-toggle { display: inline-block; }
  .session { display: none; flex-basis: 100%; flex-direction: column; align-items: stretch; gap: .25rem; }
  .session.open { display: flex; }
  .session > a { padding: .6rem 0; }
  .app-main { padding: 1rem 1rem 2rem; }
  .app-footer { padding-bottom: calc(9rem + env(safe-area-inset-bottom)); }
}

/* S8 : lien d'évitement, caché au-dessus de l'écran tant qu'il n'a pas le focus */
.skip-link { position: absolute; left: .5rem; top: -3rem; padding: .5rem 1rem;
             background: var(--primary); color: #fff; border-radius: var(--radius);
             z-index: 100; text-decoration: none; }
.skip-link:focus { top: .5rem; }

/* S8 : pas de cadre autour de toute la page quand le focus y est placé par le code */
.app-main:focus { outline: none; }
</style>