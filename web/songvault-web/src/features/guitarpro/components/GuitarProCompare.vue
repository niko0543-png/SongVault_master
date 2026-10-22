<script setup lang="ts">
import { computed, markRaw, ref, shallowRef, watch } from 'vue'
import { importer, Settings, type model } from '@coderline/alphatab'
import type { SongVersion } from '@/features/versions/types'
import { filesApi } from '@/features/files/api/filesApi'
import { t } from '@/shared/i18n'
import GuitarProViewer from './GuitarProViewer.vue'
import { toPlainScore } from '../compare/plain'
import { runDiff } from '../compare/runDiff'
import { highlight } from '../compare/highlight'
import { describeBar, describeGlobal, targetBar } from '../compare/describe'
import { guitarProFiles, partnerFile } from '../compare/files'
import type { ScoreDiff } from '../compare/scoreDiff'

/**
 * #30 : différences entre les fichiers Guitar Pro de deux versions (ADR 0035), de A (avant) vers B (après),
 * comme les paroles (#9). La partition de B est colorée ; la liste dit les mêmes différences en mots,
 * et un clic place la partition sur la mesure. Chargé à la demande par l'écran de comparaison.
 */
const props = defineProps<{ songId: string; a: SongVersion; b: SongVersion }>()

const filesA = computed(() => guitarProFiles(props.a.files))
const filesB = computed(() => guitarProFiles(props.b.files))
const fileBId = ref(filesB.value[0]?.id ?? '')
const fileAId = ref(partnerFile(filesA.value, filesB.value[0])?.id ?? '')
const fileA = computed(() => filesA.value.find((f) => f.id === fileAId.value))
const fileB = computed(() => filesB.value.find((f) => f.id === fileBId.value))

const status = ref<'loading' | 'ready' | 'error'>('loading')
const diff = shallowRef<ScoreDiff | null>(null)
const scoreB = shallowRef<model.Score | null>(null)
const trackB = ref(0)
const viewer = ref<InstanceType<typeof GuitarProViewer> | null>(null)

const barCount = computed(() => scoreB.value?.masterBars.length ?? 0)
const current = computed(() => diff.value?.tracks.find((track) => track.trackB === trackB.value))

async function read(versionId: string, fileId: string): Promise<model.Score> {
  const response = await fetch(filesApi.contentUrl(props.songId, versionId, fileId))   // suit la redirection vers le stockage
  if (!response.ok) throw new Error(String(response.status))
  return importer.ScoreLoader.loadScoreFromBytes(new Uint8Array(await response.arrayBuffer()), new Settings())
}

let run = 0
async function compare() {
  const id = ++run
  status.value = 'loading'
  if (!fileA.value || !fileB.value) return
  try {
    const [a, b] = await Promise.all([read(props.a.id, fileA.value.id), read(props.b.id, fileB.value.id)])
    const result = await runDiff(toPlainScore(a), toPlainScore(b))     // dans un Web Worker
    if (id !== run) return                                              // un autre fichier a été choisi entre-temps
    highlight(b, result)
    diff.value = result
    trackB.value = result.tracks[0]?.trackB ?? 0
    scoreB.value = markRaw(b)
    status.value = 'ready'
  } catch {
    if (id === run) status.value = 'error'
  }
}

watch([fileAId, fileBId], compare, { immediate: true })

function goTo(bar: number) {
  viewer.value?.goToBar(bar)
}
</script>

<template>
  <section class="gp-compare" :aria-label="t('guitarpro.compare.title')" :aria-busy="status === 'loading'">
    <h2>{{ t('guitarpro.compare.title') }}</h2>

    <div v-if="filesA.length > 1 || filesB.length > 1" class="files">
      <label class="inline">
        {{ t('guitarpro.compare.fileA') }}
        <select v-model="fileAId">
          <option v-for="f in filesA" :key="f.id" :value="f.id">{{ f.originalFileName }}</option>
        </select>
      </label>
      <label class="inline">
        {{ t('guitarpro.compare.fileB') }}
        <select v-model="fileBId">
          <option v-for="f in filesB" :key="f.id" :value="f.id">{{ f.originalFileName }}</option>
        </select>
      </label>
    </div>

    <p v-if="status === 'loading'" class="muted" role="status">{{ t('guitarpro.compare.loading') }}</p>
    <p v-else-if="status === 'error'" class="field-error" role="alert">{{ t('guitarpro.compare.failed') }}</p>

    <template v-if="status === 'ready' && diff && scoreB">
      <p class="muted"><small>{{ t('guitarpro.compare.direction', { fileA: fileA?.originalFileName ?? '', fileB: fileB?.originalFileName ?? '' }) }}</small></p>

      <div v-if="diff.global.length > 0" data-testid="gp-global">
        <h3>{{ t('guitarpro.compare.global') }}</h3>
        <ul>
          <li v-for="(change, i) in diff.global" :key="i">{{ describeGlobal(change) }}</li>
        </ul>
      </div>

      <label v-if="diff.tracks.length > 1" class="inline">
        {{ t('guitarpro.compare.track') }}
        <select v-model.number="trackB">
          <option v-for="track in diff.tracks" :key="track.trackB" :value="track.trackB">{{ track.name }}</option>
        </select>
      </label>

      <p role="status">{{ t('guitarpro.compare.count', { count: current?.changes.length ?? 0 }) }}</p>
      <ol v-if="current && current.changes.length > 0" class="changes" :aria-label="t('guitarpro.compare.list')" data-testid="gp-changes">
        <li v-for="(change, i) in current.changes" :key="i" :class="`change-${change.kind}`">
          <button type="button" class="link" :title="t('guitarpro.compare.goTo', { bar: targetBar(change, barCount) + 1 })"
                  @click="goTo(targetBar(change, barCount))">
            {{ describeBar(change, barCount) }}
          </button>
        </li>
      </ol>
      <p class="muted"><small>{{ t('guitarpro.compare.legend') }}</small></p>

      <GuitarProViewer ref="viewer" :score="scoreB" :track="trackB" :label="fileB?.originalFileName ?? ''" />
    </template>
  </section>
</template>

<style scoped>
.gp-compare { display: grid; gap: .5rem; min-width: 0; }
.gp-compare h2, .gp-compare h3 { margin: 0; }
.files { display: flex; flex-wrap: wrap; gap: 1rem; }
.inline { display: inline-flex; gap: .35rem; align-items: center; width: auto; }
.inline select { width: auto; }
.changes { margin: 0; padding-left: 1.5rem; max-height: 14rem; overflow-y: auto; }
/* Le même code que dans la partition (highlight.ts), en plus du texte : la couleur n'est jamais seule */
.change-added::marker { color: #1a7f37; }
.change-modified::marker { color: #b35900; }
.link { background: none; border: 0; padding: .15rem 0; color: var(--primary); text-decoration: underline; cursor: pointer; font: inherit; text-align: left; }
</style>