<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { releasesApi, toChoices } from '../api/releasesApi'
import {
  COVER_ACCEPT, COVER_MAX_BYTES, RELEASE_MAX_TRACKS, releaseTypeLabels,
  type Release, type ReleaseInput, type ReleaseTrack, type TrackChoice,
} from '../types'
import { formatReleaseDate } from '../format'
import ReleaseForm from '../components/ReleaseForm.vue'
import ReleaseExportSection from '../components/ReleaseExportSection.vue'
import ShareLinksPanel from '@/features/sharing/components/ShareLinksPanel.vue'
import { rightsApi } from '@/features/rights/api/rightsApi'
import { songsApi } from '@/features/songs/api/songsApi'
import type { Song } from '@/features/songs/types'
import { versionsApi } from '@/features/versions/api/versionsApi'
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

// ---- Titre, type, date ----
const isEditing = ref(false)
const detailsError = ref<string | null>(null)
const savingDetails = ref(false)

async function onSaveDetails(input: ReleaseInput) {
  if (!release.value) return
  savingDetails.value = true
  detailsError.value = null
  try {
    await releasesApi.update(release.value.id, input)
    release.value = { ...release.value, ...input }
    isEditing.value = false
  } catch (e) {
    detailsError.value = getErrorMessage(e)
  } finally {
    savingDetails.value = false
  }
}

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

function onChangeVersion(track: ReleaseTrack, event: Event) {
  if (!release.value) return
  const versionId = (event.target as HTMLSelectElement).value
  void saveTracks(toChoices(release.value).map((c) => (c.songId === track.songId ? { ...c, versionId } : c)))
}

function onRemoveTrack(track: ReleaseTrack) {
  if (!release.value) return
  const rest = release.value.tracks.filter((t) => t.songId !== track.songId)
  void saveTracks(toChoices({ tracks: rest }), rest)
}

function versionLabel(track: ReleaseTrack): string {
  const v = track.versions.find((x) => x.id === track.versionId)
  return v ? `v${v.number} · ${v.title} (${statusLabels[v.status]})` : t('releases.detail.unknownVersion')
}

// ---- Ajout d'un morceau ----
const search = ref('')
const results = ref<Song[]>([])
const searched = ref(false)
const isFull = computed(() => (release.value?.tracks.length ?? 0) >= RELEASE_MAX_TRACKS)
const candidates = computed(() =>
  results.value.filter((s) => !release.value?.tracks.some((t) => t.songId === s.id)))

async function onSearch() {
  tracksError.value = null
  try {
    results.value = (await songsApi.list({ search: search.value.trim() || undefined, pageSize: 10 })).items
    searched.value = true
  } catch (e) {
    tracksError.value = getErrorMessage(e)
  }
}

/** Ajoute le morceau en fin de liste, dans sa dernière version ; la version se change ensuite. */
async function onAdd(song: Song) {
  if (!release.value) return
  tracksError.value = null
  try {
    const versions = await versionsApi.list(song.id)
    const latest = versions.reduce<(typeof versions)[number] | null>(
      (best, v) => (best && best.number > v.number ? best : v), null)
    if (!latest) {
      tracksError.value = t('releases.detail.noVersion', { title: song.title })
      return
    }
    await saveTracks([...toChoices(release.value), { songId: song.id, versionId: latest.id }])
  } catch (e) {
    tracksError.value = getErrorMessage(e)
  }
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
        <div v-if="canWrite && !isEditing" class="actions">
          <button type="button" class="secondary" @click="isEditing = true">{{ t('releases.detail.edit') }}</button>
          <button type="button" class="danger" @click="onDelete">{{ t('common.actions.delete') }}</button>
        </div>
      </header>
      <p v-if="detailsError && !isEditing" class="field-error" role="alert">{{ detailsError }}</p>
      <SectionCard v-if="isEditing" :title="t('releases.detail.editTitle')">
        <ReleaseForm :initial="{ title: release.title, type: release.type, releaseDate: release.releaseDate }"
                     :submit-label="t('common.actions.save')" :submitting="savingDetails" :error="detailsError"
                     @submit="onSaveDetails" @cancel="isEditing = false" />
      </SectionCard>

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
                <RouterLink :to="{ name: 'song-detail', params: { songId: item.songId } }">{{ item.songTitle }}</RouterLink>
                <select v-if="canWrite" :value="item.versionId" :disabled="savingTracks"
                        :aria-label="t('releases.detail.versionOf', { songTitle: item.songTitle })" @change="onChangeVersion(item, $event)">
                  <option v-for="v in item.versions" :key="v.id" :value="v.id">
                    v{{ v.number }} · {{ v.title }} ({{ statusLabels[v.status] }})
                  </option>
                </select>
                <span v-else class="muted">{{ versionLabel(item) }}</span>
                <button v-if="canWrite" type="button" class="secondary" :disabled="savingTracks"
                        :aria-label="t('releases.detail.removeTrack', { songTitle: item.songTitle })" @click="onRemoveTrack(item)">{{ t('releases.detail.remove') }}</button>
              </div>
            </template>
          </ReorderableList>
          <p v-if="savingTracks" class="muted" role="status">{{ t('common.actions.saving') }}</p>

          <form v-if="canWrite" class="add-track" role="search" @submit.prevent="onSearch">
            <label for="track-search">{{ t('releases.detail.addSong') }}</label>
            <div class="search-row">
              <input id="track-search" v-model="search" type="search" :placeholder="t('releases.detail.searchPlaceholder')" :disabled="isFull" />
              <button type="submit" class="secondary" :disabled="isFull">{{ t('releases.detail.search') }}</button>
            </div>
            <p v-if="isFull" class="muted">{{ t('releases.detail.maxTracks', { releaseMaxTracks: RELEASE_MAX_TRACKS }) }}</p>
            <ul v-if="searched" class="results">
              <li v-for="song in candidates" :key="song.id">
                <span>{{ song.title }}<small v-if="song.artist" class="muted"> · {{ song.artist }}</small></span>
                <button type="button" class="secondary" :disabled="savingTracks || isFull" @click="onAdd(song)">{{ t('releases.detail.add') }}</button>
              </li>
              <li v-if="candidates.length === 0" class="muted">{{ t('releases.detail.noOtherSong') }}</li>
            </ul>
          </form>
        </SectionCard>
      </div>
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
.track { display: grid; grid-template-columns: 2rem minmax(0, 1fr) minmax(0, 14rem) auto; gap: .5rem; align-items: center; }
.track a { overflow-wrap: anywhere; }
.number { color: var(--muted); text-align: right; }
.add-track { margin-top: 1rem; display: grid; gap: .4rem; }
.search-row { display: flex; gap: .5rem; }
.results { list-style: none; padding: 0; margin: 0; display: grid; gap: .3rem; }
.results li { display: flex; justify-content: space-between; align-items: center; gap: .5rem; }
@media (max-width: 640px) { .track { grid-template-columns: 2rem 1fr; } }
</style>