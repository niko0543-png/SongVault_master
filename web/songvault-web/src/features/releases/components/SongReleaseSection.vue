<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { RouterLink } from 'vue-router'
import { releasesApi } from '../api/releasesApi'
import {
  RELEASE_LABEL_MAX, UPC_INPUT_MAX, releaseTypeLabels,
  type KeepVersions, type Release, type ReleaseSummary, type SongReleaseResult,
} from '../types'
import { formatReleaseDate } from '../format'
import StreamingLinksEditor from './StreamingLinksEditor.vue'
import StreamingLinkList from './StreamingLinkList.vue'
import type { SongVersionStatus } from '@/features/versions/types'
import { useBandStore } from '@/features/bands/stores/bandStore'
import { useSongsStore } from '@/features/songs/stores/songsStore'
import SectionCard from '@/shared/components/SectionCard.vue'
import { getErrorMessage } from '@/shared/api/ApiError'
import { t } from '@/shared/i18n'

/**
 * #38 (ADR 0044) : les infos de sortie, sur la dernière version d'un morceau au statut Final. Les enregistrer fait
 * sortir le morceau : il quitte la liste des morceaux pour l'onglet Sorties. Avant d'envoyer, on demande si l'on garde
 * toutes les versions ou seulement celle qui sort.
 */
const props = defineProps<{
  songId: string
  songTitle: string
  versionId: string
  versionNumber: number
  versionStatus: SongVersionStatus
  /** La version affichée est la dernière du morceau */
  isLatest: boolean
  /** Les autres versions en place du morceau (celles que « seulement la dernière » écarte) */
  otherVersionCount: number
  /** Sortie où le morceau est paru ; null tant qu'il est en travail */
  releaseId: string | null
}>()
const emit = defineEmits<{ released: [result: SongReleaseResult]; unreleased: [] }>()

const bands = useBandStore()
const songs = useSongsStore()
const canWrite = computed(() => bands.can('write'))
const isOwner = computed(() => bands.can('admin'))

// ================= Morceau sorti : sa sortie =================
const release = ref<Release | null>(null)
const loadError = ref<string | null>(null)

watch(() => props.releaseId, async (id) => {
  release.value = null
  loadError.value = null
  if (!id) return
  try {
    release.value = await releasesApi.get(id)
  } catch (e) {
    loadError.value = getErrorMessage(e)
  }
}, { immediate: true })

const isUnreleasing = ref(false)
async function onUnrelease() {
  isUnreleasing.value = true
  loadError.value = null
  try {
    await releasesApi.unreleaseSong(props.songId)
    songs.invalidate()
    result.value = null
    emit('unreleased')
  } catch (e) {
    loadError.value = getErrorMessage(e)
  } finally {
    isUnreleasing.value = false
  }
}

// ================= Formulaire =================
const canRelease = computed(() => !props.releaseId && props.isLatest && props.versionStatus === 'Final' && canWrite.value)
const releases = ref<ReleaseSummary[]>([])
watch(canRelease, async (yes) => {
  if (!yes || releases.value.length) return
  try {
    releases.value = await releasesApi.list()
  } catch {
    /* sans la liste, le morceau sort en single : rien d'autre ne manque */
  }
}, { immediate: true })

const form = reactive({
  releaseId: '',                // '' = nouveau single au titre du morceau
  releaseDate: '',
  label: '',
  upc: '',
  links: [''] as string[],
})

/** Rejoindre une sortie existante : ses infos de sortie remplissent le formulaire. */
async function onChooseRelease() {
  if (!form.releaseId) return
  try {
    const chosen = await releasesApi.get(form.releaseId)
    form.releaseDate = chosen.releaseDate ?? form.releaseDate
    form.label = chosen.label ?? form.label
    form.upc = chosen.upc ?? form.upc
    if (chosen.streamingLinks.length) form.links = chosen.streamingLinks.map((l) => l.url)
  } catch {
    /* le formulaire reste tel quel */
  }
}

const step = ref<'form' | 'confirm'>('form')
const keep = ref<KeepVersions>('All')
const purgeNow = ref(false)
const isSaving = ref(false)
const saveError = ref<string | null>(null)
const result = ref<SongReleaseResult | null>(null)

watch(keep, (value) => { if (value === 'All') purgeNow.value = false })

function onNext() {
  saveError.value = null
  // Rien à demander quand la version qui sort est la seule
  if (props.otherVersionCount === 0) void onConfirm()
  else step.value = 'confirm'
}

async function onConfirm() {
  isSaving.value = true
  saveError.value = null
  try {
    result.value = await releasesApi.releaseSong(props.songId, {
      versionId: props.versionId,
      releaseId: form.releaseId || null,
      releaseDate: form.releaseDate || null,
      label: form.label.trim() || null,
      upc: form.upc.trim() || null,
      streamingLinks: form.links.map((l) => l.trim()).filter((l) => l !== ''),
      keep: props.otherVersionCount === 0 ? 'All' : keep.value,
      purgeNow: purgeNow.value,
    })
    songs.invalidate()
    step.value = 'form'
    emit('released', result.value)
  } catch (e) {
    saveError.value = getErrorMessage(e)
  } finally {
    isSaving.value = false
  }
}

/** Ce qui est arrivé aux autres versions, en une phrase. */
const removedText = computed(() => {
  const r = result.value
  if (!r || r.removedVersions.length === 0) return null
  const versions = r.removedVersions.map((n) => `v${n}`).join(', ')
  return r.purged ? t('releases.songRelease.purged', { versions }) : t('releases.songRelease.trashed', { versions })
})

const keptTexts = computed(() => (result.value?.keptVersions ?? []).map((k) => {
  const places = [...k.releases.map((r) => `« ${r} »`), ...k.setlists.map((s) => `« ${s} »`)].join(', ')
  return t('releases.songRelease.kept', { number: k.number, places })
}))
</script>

<!-- Trois cas : morceau sorti ; dernière version pas encore Final (on dit comment faire) ; dernière version Final -->
<template>
  <SectionCard v-if="releaseId" :title="t('releases.songRelease.title')" data-testid="song-release">
    <p v-if="loadError" class="field-error" role="alert">{{ loadError }}</p>
    <template v-if="release">
      <p>
        {{ t('releases.songRelease.releasedOn') }}
        <RouterLink :to="{ name: 'release-detail', params: { releaseId: release.id } }">{{ release.title }}</RouterLink>
        <span class="muted"> · {{ releaseTypeLabels[release.type] }} · {{ formatReleaseDate(release.releaseDate) }}</span>
      </p>
      <p v-if="release.label || release.upc" class="muted">
        <small>
          <template v-if="release.label">{{ t('releases.distribution.label') }} : {{ release.label }}</template>
          <template v-if="release.label && release.upc"> · </template>
          <template v-if="release.upc">{{ t('releases.distribution.upc') }} : {{ release.upc }}</template>
        </small>
      </p>
      <StreamingLinkList :links="release.streamingLinks" />
    </template>
    <p v-if="removedText" class="muted" role="status">{{ removedText }}</p>
    <p v-for="text in keptTexts" :key="text" class="muted">{{ text }}</p>
    <button v-if="canWrite" type="button" class="secondary" :disabled="isUnreleasing" @click="onUnrelease">
      {{ t('releases.songRelease.unrelease') }}
    </button>
  </SectionCard>

  <SectionCard v-else-if="isLatest && canWrite && versionStatus !== 'Final'" :title="t('releases.songRelease.title')">
    <p class="muted">{{ t('releases.songRelease.needsFinal') }}</p>
  </SectionCard>

  <SectionCard v-else-if="canRelease" :title="t('releases.songRelease.title')" data-testid="song-release-form">
    <form v-if="step === 'form'" class="release-info" novalidate @submit.prevent="onNext">
      <p class="muted">{{ t('releases.songRelease.intro', { number: versionNumber }) }}</p>
      <div class="row">
        <div class="field">
          <label for="sr-release">{{ t('releases.songRelease.release') }}</label>
          <select id="sr-release" v-model="form.releaseId" @change="onChooseRelease">
            <option value="">{{ t('releases.songRelease.newSingle', { title: songTitle }) }}</option>
            <option v-for="r in releases" :key="r.id" :value="r.id">{{ r.title }} ({{ releaseTypeLabels[r.type] }})</option>
          </select>
        </div>
        <div class="field">
          <label for="sr-date">{{ t('releases.form.date') }}</label>
          <input id="sr-date" v-model="form.releaseDate" type="date" />
        </div>
      </div>
      <div class="row">
        <div class="field">
          <label for="sr-label">{{ t('releases.distribution.label') }} <small class="muted">{{ t('releases.distribution.optional') }}</small></label>
          <input id="sr-label" v-model="form.label" :maxlength="RELEASE_LABEL_MAX" autocomplete="off" />
        </div>
        <div class="field">
          <label for="sr-upc">{{ t('releases.distribution.upc') }} <small class="muted">{{ t('releases.distribution.optional') }}</small></label>
          <input id="sr-upc" v-model="form.upc" :maxlength="UPC_INPUT_MAX" inputmode="numeric" autocomplete="off" />
        </div>
      </div>
      <StreamingLinksEditor v-model="form.links" id-prefix="sr" />
      <p v-if="saveError" class="field-error" role="alert">{{ saveError }}</p>
      <div class="buttons">
        <button type="submit" :disabled="isSaving">
          {{ isSaving ? t('common.actions.saving') : t('releases.songRelease.save') }}
        </button>
      </div>
    </form>

    <!-- La question : toutes les versions, ou seulement celle qui sort -->
    <form v-else class="release-info" @submit.prevent="onConfirm">
      <fieldset class="bare keep" data-testid="keep-versions">
        <legend>{{ t('releases.songRelease.keepQuestion') }}</legend>
        <label class="choice">
          <input v-model="keep" type="radio" value="All" />
          <span>{{ t('releases.songRelease.keepAll') }}
            <small class="muted">{{ t('releases.songRelease.keepAllHint', { count: otherVersionCount + 1 }) }}</small></span>
        </label>
        <label class="choice">
          <input v-model="keep" type="radio" value="LatestOnly" />
          <span>{{ t('releases.songRelease.keepLatest', { number: versionNumber }) }}
            <small class="muted">{{ t('releases.songRelease.keepLatestHint', { count: otherVersionCount }) }}</small></span>
        </label>
        <label v-if="isOwner && keep === 'LatestOnly'" class="choice purge">
          <input v-model="purgeNow" type="checkbox" />
          <span>{{ t('releases.songRelease.purgeNow') }}
            <small class="muted">{{ t('releases.songRelease.purgeNowHint') }}</small></span>
        </label>
      </fieldset>
      <p v-if="saveError" class="field-error" role="alert">{{ saveError }}</p>
      <div class="buttons">
        <button type="button" class="secondary" :disabled="isSaving" @click="step = 'form'">{{ t('releases.songRelease.back') }}</button>
        <button type="submit" :class="{ danger: purgeNow }" :disabled="isSaving">
          {{ isSaving ? t('common.actions.saving') : t('releases.songRelease.confirm') }}
        </button>
      </div>
    </form>
  </SectionCard>
</template>

<style scoped>
.release-info { display: grid; gap: .75rem; }
.release-info p { margin: 0; }
.row { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
@media (max-width: 600px) { .row { grid-template-columns: 1fr; } }
.bare { border: 0; padding: 0; margin: 0; min-width: 0; }
.keep { display: grid; gap: .6rem; }
.keep legend { font-weight: 600; padding: 0; margin-bottom: .25rem; }
.choice { display: flex; gap: .5rem; align-items: flex-start; font-weight: normal; }
.choice input { width: auto; margin-top: .2rem; }
.choice small { display: block; }
.purge { margin-left: 1.5rem; }
.buttons { display: flex; gap: .75rem; justify-content: flex-end; flex-wrap: wrap; }
</style>
