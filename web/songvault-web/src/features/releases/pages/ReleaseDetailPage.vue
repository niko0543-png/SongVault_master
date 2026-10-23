<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { releasesApi } from '../api/releasesApi'
import {
  COVER_ACCEPT, COVER_MAX_BYTES, releaseTypeLabels,
  type Release, type ReleaseTrack, type TrackChoice,
} from '../types'
import { formatReleaseDate } from '../format'
import ReleaseExportSection from '../components/ReleaseExportSection.vue'
import ReleaseDistributionSection from '../components/ReleaseDistributionSection.vue'
import ShareLinksPanel from '@/features/sharing/components/ShareLinksPanel.vue'
import { rightsApi } from '@/features/rights/api/rightsApi'
import { statusLabels } from '@/features/versions/types'
import { useBandStore } from '@/features/bands/stores/bandStore'
import LoadingState from '@/shared/components/LoadingState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import SectionCard from '@/shared/components/SectionCard.vue'
import ReorderableList from '@/shared/components/ReorderableList.vue'
import { ApiError, getErrorMessage } from '@/shared/api/ApiError'
import { useConfirm } from '@/shared/composables/useConfirm'
import { useToast } from '@/shared/composables/useToast'
import { formatFileSize } from '@/shared/utils/format'
import { t } from '@/shared/i18n'

const props = defineProps<{ releaseId: string }>()
const bands = useBandStore()
const router = useRouter()
const { confirm } = useConfirm()
const toast = useToast()
const canWrite = computed(() => bands.can('write'))

// ---- Chargement ----
const release = ref<Release | null>(null)
const isLoading = ref(true)
const error = ref<unknown>(null)
const isNotFound = computed(() => error.value instanceof ApiError && error.value.status === 404)

async function load(id: string) {
  isLoading.value = true
  error.value = null
  try {
    release.value = await releasesApi.get(id)
  } catch (e) {
    error.value = e
  } finally {
    isLoading.value = false
  }
}
watch(() => props.releaseId, load, { immediate: true })

// #39 : titre, type, date et infos de sortie ne se saisissent plus ici, mais depuis la dernière version finale
// d'un morceau (section « Sortie ») ; la page garde la pochette, l'ordre des pistes, l'archive et le partage.
const detailsError = ref<string | null>(null)

async function onDelete() {
  if (!release.value) return
  const { id, title } = release.value
  const confirmed = await confirm({
    title: t('releases.detail.deleteTitle'),
    message: t('releases.detail.deleteMessage', { title }),
    confirmLabel: t('common.actions.delete'),
    danger: true,
  })
  if (!confirmed) return
  try {
    await releasesApi.remove(id)
    toast.success(t('releases.detail.deleted', { title }))
    await router.push({ name: 'releases' })
  } catch (e) {
    detailsError.value = getErrorMessage(e)
  }
}

// ---- Pistes : chaque changement enregistre la liste complète (ADR 0016) ----
const savingTracks = ref(false)
const tracksError = ref<string | null>(null)

/** Enregistre cet ordre ; shown est ce que la page affiche pendant l'enregistrement. */
async function saveTracks(choices: TrackChoice[], shown?: ReleaseTrack[]) {
  const current = release.value
  if (!current) return
  savingTracks.value = true
  tracksError.value = null
  if (shown) release.value = { ...current, tracks: shown.map((t, i) => ({ ...t, trackNumber: i + 1 })) }
  try {
    release.value = await releasesApi.setTracks(current.id, choices)
  } catch (e) {
    release.value = current                       // refusé : on revient à l'ordre enregistré
    tracksError.value = getErrorMessage(e)
  } finally {
    savingTracks.value = false
  }
}

function onMove(tracks: ReleaseTrack[]) {
  void saveTracks(tracks.map((t) => ({ songId: t.songId, versionId: t.versionId })), tracks)
}

function versionLabel(track: ReleaseTrack): string {
  const v = track.versions.find((x) => x.id === track.versionId)
  return v ? `v${v.number} · ${v.title} (${statusLabels[v.status]})` : t('releases.detail.unknownVersion')
}

// ---- Pochette ----
const coverError = ref<string | null>(null)
const savingCover = ref(false)

async function onCoverChosen(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file || !release.value) return
  coverError.value = null
  if (file.size > COVER_MAX_BYTES) {
    coverError.value = t('releases.detail.coverTooLarge', { coverMaxBytes: formatFileSize(COVER_MAX_BYTES) })
    input.value = ''
    return
  }
  savingCover.value = true
  try {
    await releasesApi.uploadCover(release.value.id, file)
    release.value = await releasesApi.get(release.value.id)     // hasCover et updatedAt à jour
  } catch (e) {
    coverError.value = getErrorMessage(e)
  } finally {
    savingCover.value = false
    input.value = ''
  }
}

async function onRemoveCover() {
  if (!release.value) return
  savingCover.value = true
  coverError.value = null
  try {
    await releasesApi.removeCover(release.value.id)
    release.value = { ...release.value, hasCover: false }
  } catch (e) {
    coverError.value = getErrorMessage(e)
  } finally {
    savingCover.value = false
  }
}
</script>

<template>
  <section>
    <RouterLink :to="{ name: 'releases' }">{{ t('releases.detail.back') }}</RouterLink>

    <LoadingState v-if="isLoading" />
    <div v-else-if="isNotFound" class="card" role="alert"><p>{{ t('releases.detail.notFound') }}</p></div>
    <ErrorState v-else-if="error" :message="getErrorMessage(error)" @retry="load(releaseId)" />

    <template v-else-if="release">
      <header class="page-header">
        <div>
          <h1>{{ release.title }}</h1>
          <p class="muted">{{ releaseTypeLabels[release.type] }} · {{ formatReleaseDate(release.releaseDate) }}</p>
        </div>
        <div v-if="canWrite" class="actions">
          <button type="button" class="danger" @click="onDelete">{{ t('common.actions.delete') }}</button>
        </div>
      </header>
      <p v-if="detailsError" class="field-error" role="alert">{{ detailsError }}</p>

      <div class="layout">
        <SectionCard :title="t('releases.detail.cover')">
          <img v-if="release.hasCover" :src="releasesApi.coverUrl(release.id, release.updatedAt)"
               :alt="t('releases.detail.coverOf', { title: release.title })" class="cover" />
          <p v-else class="muted">{{ t('releases.detail.noCover') }}</p>
          <template v-if="canWrite">
            <label class="file-label">
              {{ release.hasCover ? t('releases.detail.replaceCover') : t('releases.detail.addCover') }}
              <small class="muted">{{ t('releases.detail.coverRules', { coverMaxBytes: formatFileSize(COVER_MAX_BYTES) }) }}</small>
              <input type="file" :accept="COVER_ACCEPT" :disabled="savingCover" @change="onCoverChosen" />
            </label>
            <button v-if="release.hasCover" type="button" class="secondary" :disabled="savingCover"
                    @click="onRemoveCover">{{ t('releases.detail.removeCover') }}</button>
          </template>
          <p v-if="coverError" class="field-error" role="alert">{{ coverError }}</p>
        </SectionCard>

        <SectionCard :title="t('releases.detail.tracks', { count: release.tracks.length })">
          <template v-if="release.tracks.length" #actions>
            <a :href="rightsApi.releaseCsvUrl(release.id)" download>{{ t('releases.detail.exportCredits') }}</a>
          </template>
          <p v-if="tracksError" class="field-error" role="alert">{{ tracksError }}</p>
          <p v-if="release.tracks.length === 0" class="muted">{{ t('releases.detail.noTracks') }}</p>
          <ReorderableList v-else :items="release.tracks" :item-key="(t) => t.songId" :item-label="(t) => t.songTitle"
                           :label="t('releases.detail.tracksLabel')" :disabled="!canWrite || savingTracks" @move="onMove">
            <template #default="{ item }">
              <div class="track" data-testid="release-track">
                <span class="number">{{ item.trackNumber }}.</span>
                <span class="title">
                  <RouterLink :to="{ name: 'song-detail', params: { songId: item.songId } }">{{ item.songTitle }}</RouterLink>
                  <small v-if="item.isReleasedHere" class="released-badge">{{ t('releases.detail.releasedHere') }}</small>
                </span>
<span class="muted">{{ versionLabel(item) }}</span>
              </div>
            </template>
          </ReorderableList>
          <p v-if="savingTracks" class="muted" role="status">{{ t('common.actions.saving') }}</p>

          <p class="muted"><small>{{ t('releases.detail.fromSongs') }}</small></p>
        </SectionCard>
      </div>
      <ReleaseDistributionSection :release="release" :can-write="false" />
      <ReleaseExportSection :release-id="release.id" :can-write="canWrite" />
      <ShareLinksPanel v-if="canWrite" :target="{ type: 'Release', id: release.id }" />
    </template>
  </section>
</template>

<style scoped>
.actions { display: flex; gap: .75rem; align-items: center; }
.layout { display: grid; grid-template-columns: minmax(200px, 1fr) 2fr; gap: 1rem; align-items: start; }
@media (max-width: 800px) { .layout { grid-template-columns: 1fr; } }
.cover { width: 100%; max-width: 320px; aspect-ratio: 1; object-fit: cover; border-radius: var(--radius); display: block; margin-bottom: .75rem; }
.file-label { display: grid; gap: .25rem; margin-bottom: .5rem; }
.track { display: grid; grid-template-columns: 2rem minmax(6rem, 1fr) auto; gap: .5rem; align-items: center; }
.track a { overflow-wrap: anywhere; }
.number { color: var(--muted); text-align: right; }
.title { min-width: 0; }
.released-badge { display: inline-block; padding: 0 .4rem; border: 1px solid currentColor; border-radius: 999px; font-size: .75rem; color: var(--muted); }
@media (max-width: 640px) { .track { grid-template-columns: 2rem 1fr; } }
</style>