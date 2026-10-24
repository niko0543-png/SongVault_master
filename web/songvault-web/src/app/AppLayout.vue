<script setup lang="ts">
import { computed, ref, watch } from 'vue'
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
import LocaleSwitcher from '@/shared/i18n/LocaleSwitcher.vue'
import { locale, t, type Locale } from '@/shared/i18n'
import { useToast } from '@/shared/composables/useToast'

const auth = useAuthStore()
const { user, isAuthenticated } = storeToRefs(auth)
const bands = useBandStore()
const router = useRouter()
const route = useRoute()

// #14 : sur un téléphone, la navigation est repliée derrière le bouton « Menu »
const isMenuOpen = ref(false)
watch(() => route.fullPath, () => { isMenuOpen.value = false })   // une page choisie referme le menu

// Onglet en cours : la page d'un morceau, d'une version ou d'une tablature reste sous « Morceaux », etc.
const SECTIONS: Record<string, string[]> = {
  songs: ['songs', 'song-create', 'song-edit', 'song-detail', 'version-compare', 'version-detail', 'tablature'],
  releases: ['releases', 'release-detail'],
  setlists: ['setlists', 'setlist-detail'],
}
const section = computed(() => {
  const name = String(route.name ?? '')
  return Object.keys(SECTIONS).find((key) => SECTIONS[key]!.includes(name)) ?? name
})

// Changement de groupe : la liste en cache et la lecture en cours appartiennent à l'ancien groupe
watch(() => bands.activeId, (next, prev) => {
  if (prev && next !== prev) { useSongsStore().reset(); usePlayerStore().stop() }
})

// #26 : la langue change tout de suite ; si le compte ne l'enregistre pas, elle reste choisie sur cet appareil
async function onLocaleChange(next: Locale) {
  try {
    await auth.changeLocale(next)
  } catch {
    useToast().error(t('common.language.saveFailed'))
  }
}

async function onLogout() {
  isMenuOpen.value = false
  await auth.logout()
  await router.push({ name: 'login' })
}
</script>

<template>
  <!-- S8 : premier élément de la page, donc premier atteint avec Tab -->
  <a href="#main-content" class="skip-link">{{ t('layout.skipLink') }}</a>

  <header class="app-header">
    <RouterLink to="/" class="brand">🎸 SongVault</RouterLink>
    <!-- #14 : visible sous 720 px seulement (CSS) ; au-dessus, la navigation est toujours affichée -->
    <button v-if="isAuthenticated" type="button" class="secondary menu-toggle" aria-controls="app-nav"
            :aria-expanded="isMenuOpen" @click="isMenuOpen = !isMenuOpen">
      {{ isMenuOpen ? t('layout.menu.close') : t('layout.menu.open') }}
    </button>
    <nav v-if="isAuthenticated" id="app-nav" class="session" :class="{ open: isMenuOpen }" :aria-label="t('layout.nav.label')">
      <BandSwitcher v-if="bands.isLoaded && bands.bands.length" />
      <!-- Onglet « Morceaux » : la liste des morceaux du groupe, jusqu'ici atteinte seulement par le logo -->
      <RouterLink v-if="bands.activeId" :class="{ active: section === 'songs' }" :to="{ name: 'songs', params: { bandId: bands.activeId } }">{{ t('layout.nav.songs') }}</RouterLink>
      <RouterLink v-if="bands.activeId" :class="{ active: section === 'releases' }" :to="{ name: 'releases', params: { bandId: bands.activeId } }">{{ t('layout.nav.releases') }}</RouterLink>
      <RouterLink v-if="bands.activeId" :class="{ active: section === 'setlists' }" :to="{ name: 'setlists', params: { bandId: bands.activeId } }">{{ t('layout.nav.setlists') }}</RouterLink>
      <RouterLink v-if="bands.activeId && bands.can('write')" :class="{ active: section === 'record' }" :to="{ name: 'record', params: { bandId: bands.activeId } }">{{ t('layout.nav.record') }}</RouterLink>
      <RouterLink v-if="bands.activeId" :class="{ active: section === 'activity' }" :to="{ name: 'activity', params: { bandId: bands.activeId } }">{{ t('layout.nav.activity') }}</RouterLink>
      <RouterLink v-if="bands.activeId" :class="{ active: section === 'band-members' }" :to="{ name: 'band-members', params: { bandId: bands.activeId } }">{{ t('layout.nav.members') }}</RouterLink>
      <RouterLink v-if="bands.activeId && bands.can('write')" :class="{ active: section === 'trash' }" :to="{ name: 'trash', params: { bandId: bands.activeId } }">{{ t('layout.nav.trash') }}</RouterLink>
      <RouterLink v-if="bands.activeId && bands.can('admin')" :class="{ active: section === 'billing' }" :to="{ name: 'billing', params: { bandId: bands.activeId } }">{{ t('layout.nav.billing') }}</RouterLink>
      <RouterLink v-if="bands.activeId && bands.can('admin')" :class="{ active: section === 'audit' }" :to="{ name: 'audit', params: { bandId: bands.activeId } }">{{ t('layout.nav.audit') }}</RouterLink>
      <RouterLink :to="{ name: 'account' }" class="muted account-link" :title="t('layout.nav.account')">{{ user?.email }}</RouterLink>
      <button type="button" class="secondary" @click="onLogout">{{ t('layout.nav.logout') }}</button>
    </nav>
  </header>

  <!-- S8 : id = cible du lien et du focus après navigation ; tabindex="-1" = focalisable par le code seulement -->
  <!-- #26 : :key recrée la page quand la langue change, pour les textes calculés une fois au montage -->
  <main id="main-content" :key="locale" class="app-main" tabindex="-1">
    <RouterView />
  </main>

  <footer class="app-footer">
    <RouterLink :to="{ name: 'legal-notice' }">{{ t('layout.footer.legalNotice') }}</RouterLink>
    <RouterLink :to="{ name: 'privacy' }">{{ t('layout.footer.privacy') }}</RouterLink>
    <RouterLink :to="{ name: 'plans' }">{{ t('layout.footer.plans') }}</RouterLink>
    <RouterLink :to="{ name: 'terms-of-sale' }">{{ t('layout.footer.termsOfSale') }}</RouterLink>
    <RouterLink :to="{ name: 'status' }">{{ t('layout.footer.status') }}</RouterLink>
    <LocaleSwitcher @change="onLocaleChange" />
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
/* L'onglet de la page affichée */
.session > a.active { font-weight: 600; text-decoration: underline; text-underline-offset: .3em; }
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