<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { t } from '@/shared/i18n'
import GuitarProViewer from './GuitarProViewer.vue'
import { describeBar, describeGlobal, targetBar } from '../compare/describe'
import { useScoreCompare, type ComparedFile } from '../compare/useScoreCompare'

/**
 * #30 : différences entre les fichiers Guitar Pro de deux versions (ADR 0035), de A (avant) vers B (après),
 * comme les paroles (#9). La partition de B est colorée ; la liste dit les mêmes différences en mots,
 * et un clic place la partition sur la mesure.
 * #36 : ouvert par « Afficher les modifs » de l'écran A/B, sur la page de la tablature (ADR 0042) :
 * le lecteur y occupe toute la place (`fill`), la liste est à côté de la partition, repliable sur téléphone.
 */
const props = defineProps<{ songId: string; a: ComparedFile; b: ComparedFile; label: string }>()

const { status, diff, scoreB } = useScoreCompare(() => props.songId, () => props.a, () => props.b)
const trackB = ref(0)
const viewer = ref<InstanceType<typeof GuitarProViewer> | null>(null)
const barCount = computed(() => scoreB.value?.masterBars.length ?? 0)
const current = computed(() => diff.value?.tracks.find((track) => track.trackB === trackB.value))

watch(diff, (result) => { trackB.value = result?.tracks[0]?.trackB ?? 0 })

function goTo(bar: number) {
  viewer.value?.goToBar(bar)
}
</script>

<template>
  <div class="gp-compare" :aria-busy="status === 'loading'">
    <p v-if="status === 'loading'" class="muted" role="status">{{ t('guitarpro.compare.loading') }}</p>
    <p v-else-if="status === 'error'" class="field-error" role="alert">{{ t('guitarpro.compare.failed') }}</p>

    <template v-if="status === 'ready' && diff && scoreB">
      <details class="changes-panel" open data-testid="gp-changes-panel">
        <summary>{{ t('guitarpro.page.changes') }}</summary>
        <div class="changes-body">
          <div v-if="diff.global.length > 0" data-testid="gp-global">
            <h2>{{ t('guitarpro.compare.global') }}</h2>
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
        </div>
      </details>

      <GuitarProViewer ref="viewer" class="viewer" fill :score="scoreB" :track="trackB" :label="label" />
    </template>
  </div>
</template>

<style scoped>
/* La liste à gauche, la partition à droite ; sur téléphone, la liste au-dessus, repliable */
.gp-compare { display: grid; grid-template-columns: minmax(14rem, 20rem) minmax(0, 1fr); grid-template-rows: minmax(0, 1fr); gap: .75rem; min-height: 0; }
.gp-compare > p { grid-column: 1 / -1; margin: 0; }
.changes-panel { min-height: 0; overflow-y: auto; border: 1px solid var(--border); border-radius: var(--radius); padding: .5rem .75rem; }
.changes-panel summary { font-weight: 600; cursor: pointer; }
.changes-body { display: grid; gap: .5rem; margin-top: .5rem; }
.changes-body h2 { margin: 0; font-size: 1rem; }
.changes-body ul, .changes-body p { margin: 0; }
.inline { display: inline-flex; gap: .35rem; align-items: center; width: auto; }
.inline select { width: auto; }
.changes { margin: 0; padding-left: 1.5rem; }
/* Le même code que dans la partition (highlight.ts), en plus du texte : la couleur n'est jamais seule */
.change-added::marker { color: #1a7f37; }
.change-modified::marker { color: #b35900; }
.link { background: none; border: 0; padding: .15rem 0; color: var(--primary); text-decoration: underline; cursor: pointer; font: inherit; text-align: left; vertical-align: top; }
.viewer { min-height: 0; min-width: 0; }
@media (max-width: 899px) {
  .gp-compare { grid-template-columns: minmax(0, 1fr); grid-template-rows: auto minmax(0, 1fr); }
  .changes-panel { max-height: 30vh; }
}
</style>
