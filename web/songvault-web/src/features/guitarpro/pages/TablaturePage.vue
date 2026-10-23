<script setup lang="ts">
import { computed, defineAsyncComponent, onBeforeUnmount, onMounted, ref } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { useVersion } from '@/features/versions/composables/useVersion'
import { isReadable } from '@/features/files/types'
import { filesApi } from '@/features/files/api/filesApi'
import { useBandStore } from '@/features/bands/stores/bandStore'
import GuitarProViewer from '../components/GuitarProViewer.vue'
import type { ComparedFile } from '../compare/useScoreCompare'
import LoadingState from '@/shared/components/LoadingState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import { ApiError, getErrorMessage } from '@/shared/api/ApiError'
import { t } from '@/shared/i18n'

/**
 * #32 : le lecteur Guitar Pro sur une page à lui (ADR 0038). Elle couvre l'application, sauf le lecteur audio du bas,
 * et le lecteur y tient en un seul bloc (`fill`) : commandes, partition qui défile, pistes. Une vraie adresse :
 * on peut l'ouvrir dans un nouvel onglet ou la garder en favori. Le chargement d'alphaTab reste à la demande :
 * la page est elle-même chargée à la demande par le routeur.
 * #36 : avec `?a={version A}&aFile={fichier A}` (« Afficher les modifs » de l'écran A/B), la partition montre
 * les différences depuis ce fichier de A, et leur liste est à côté (ADR 0042).
 */
const props = defineProps<{ songId: string; versionId: string; fileId: string }>()
const GuitarProCompare = defineAsyncComponent(() => import('../components/GuitarProCompare.vue'))
const route = useRoute()

const { song, version, siblings, isLoading, error, reload } = useVersion(() => props.songId, () => props.versionId)
const bands = useBandStore()
const isNotFound = computed(() => error.value instanceof ApiError && error.value.status === 404)
const file = computed(() => version.value?.files.find((f) => f.id === props.fileId && f.fileType === 'Tablature') ?? null)
// #29 : les maquettes lisibles de la version, sur lesquelles la tablature peut se caler
const sync = computed(() => version.value && file.value
  ? {
      songId: props.songId,
      versionId: props.versionId,
      fileId: file.value.id,
      audios: version.value.files
        .filter((f) => f.fileType === 'Audio' && isReadable(f))
        .map((f) => ({ id: f.id, name: f.originalFileName })),
      canEdit: bands.can('write'),
    }
  : undefined)

// #36 : le fichier de A auquel comparer celui-ci, s'il est dans l'adresse
const query = (name: string) => (typeof route.query[name] === 'string' && route.query[name] !== '' ? route.query[name] as string : null)
const compareWith = computed<ComparedFile | null>(() => {
  const versionA = query('a')
  const fileA = query('aFile')
  return versionA && fileA ? { versionId: versionA, fileId: fileA } : null
})
const numberA = computed(() => siblings.value.find((v) => v.id === compareWith.value?.versionId)?.number ?? null)
const numberB = computed(() => version.value?.number ?? null)

// Plein écran du navigateur, s'il le permet ; la page seule y passe
const page = ref<HTMLElement | null>(null)
const canFullscreen = typeof document !== 'undefined' && document.fullscreenEnabled === true
const isFullscreen = ref(false)
const onFullscreenChange = () => { isFullscreen.value = document.fullscreenElement === page.value }
function toggleFullscreen() {
  if (document.fullscreenElement) void document.exitFullscreen()
  else void page.value?.requestFullscreen()
}

// La page derrière ne défile pas : seule la partition défile
onMounted(() => {
  document.documentElement.classList.add('tablature-open')
  document.addEventListener('fullscreenchange', onFullscreenChange)
})
onBeforeUnmount(() => {
  document.documentElement.classList.remove('tablature-open')
  document.removeEventListener('fullscreenchange', onFullscreenChange)
})
</script>

<template>
  <div ref="page" class="tablature-page" data-testid="tablature-page">
    <header class="page-head">
      <RouterLink v-if="compareWith && numberA !== null && numberB !== null" data-testid="tablature-back"
                  :to="{ name: 'version-compare', params: { songId }, query: { a: String(numberA), b: String(numberB) } }">
        ← {{ t('guitarpro.page.backToCompare') }}
      </RouterLink>
      <RouterLink v-else :to="{ name: 'version-detail', params: { songId, versionId } }" data-testid="tablature-back">
        ← {{ t('guitarpro.page.back') }}
      </RouterLink>
      <div v-if="file" class="title">
        <h1>{{ file.originalFileName }}</h1>
        <small class="muted">{{ song?.title }} · {{ version?.title }}</small>
        <small v-if="compareWith" class="changes-since" data-testid="tablature-changes-since">
          {{ numberA !== null ? t('guitarpro.page.changesSince', { number: numberA }) : t('guitarpro.page.changesSinceOther') }}
          · <RouterLink :to="{ name: 'tablature', params: { songId, versionId, fileId } }" data-testid="tablature-without-changes">{{ t('guitarpro.page.withoutChanges') }}</RouterLink>
        </small>
      </div>
      <button v-if="canFullscreen && file" type="button" class="secondary" data-testid="tablature-fullscreen"
              :aria-pressed="isFullscreen" @click="toggleFullscreen">
        {{ isFullscreen ? t('guitarpro.page.exitFullscreen') : t('guitarpro.page.fullscreen') }}
      </button>
    </header>

    <LoadingState v-if="isLoading" />
    <div v-else-if="isNotFound || (version && !file)" class="card" role="alert"><p>{{ t('guitarpro.page.notFound') }}</p></div>
    <ErrorState v-else-if="error" :message="getErrorMessage(error)" @retry="reload()" />
    <div v-else-if="file && !isReadable(file)" class="card" role="alert"><p>{{ t('guitarpro.page.unreadable') }}</p></div>
    <GuitarProCompare v-else-if="file && compareWith" :key="`${compareWith.fileId}-${file.id}`" class="viewer"
                      :song-id="songId" :a="compareWith" :b="{ versionId, fileId: file.id }" :label="file.originalFileName" />
    <GuitarProViewer v-else-if="file" :key="file.id" class="viewer" fill
                     :src="filesApi.contentUrl(songId, versionId, file.id)" :label="file.originalFileName" :sync="sync" />
  </div>
</template>

<style scoped>
/* Au-dessus de l'en-tête et du pied de page de l'application ; le lecteur audio du bas reste visible et utilisable */
.tablature-page { position: fixed; inset: 0 0 var(--audio-player-height, 0px) 0; z-index: 30; display: flex;
                  flex-direction: column; gap: .5rem; padding: .5rem 1rem; background: var(--bg, #fff); }
.tablature-page:fullscreen { inset: 0; }
.page-head { display: flex; align-items: center; gap: 1rem; flex: none; flex-wrap: wrap; }
.title { display: grid; flex: 1; min-width: 0; }
.changes-since { color: var(--text); }
.title h1 { margin: 0; font-size: 1.1rem; overflow: hidden; white-space: nowrap; text-overflow: ellipsis; }
.viewer { flex: 1; min-height: 0; }
@media (max-width: 719px) {
  .tablature-page { padding: .5rem; }
}
</style>

<style>
/* #32 : tant que la page du lecteur est ouverte, la page de l'application derrière elle ne défile pas */
html.tablature-open, html.tablature-open body { overflow: hidden; }
</style>
