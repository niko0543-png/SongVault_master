<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import type { SongFile } from '@/features/files/types'
import type { SongVersion } from '@/features/versions/types'
import { t } from '@/shared/i18n'
import { guitarProFiles, partnerFile } from '../compare/files'
import { summarize } from '../compare/summary'
import { useScoreCompare } from '../compare/useScoreCompare'

/**
 * #36 : à côté d'un fichier Guitar Pro de B, dans l'écran A/B, ce qui a changé depuis A en quelques lignes,
 * et « Afficher les modifs », qui ouvre la page de la tablature (#32) avec les différences colorées et leur liste.
 * Chargé à la demande par l'écran A/B : alphaTab n'est téléchargé que s'il y a deux fichiers Guitar Pro à comparer.
 */
const props = defineProps<{ songId: string; a: SongVersion; b: SongVersion; file: SongFile }>()

const fileA = computed(() => partnerFile(guitarProFiles(props.a.files), props.file) ?? null)
const { status, diff } = useScoreCompare(
  () => props.songId,
  () => (fileA.value ? { versionId: props.a.id, fileId: fileA.value.id } : null),
  () => ({ versionId: props.b.id, fileId: props.file.id }),
)
const lines = computed(() => (diff.value ? summarize(diff.value) : []))
</script>

<template>
  <div v-if="fileA" class="gp-summary" :aria-label="t('guitarpro.compare.summary.label', { file: file.originalFileName })"
       :aria-busy="status === 'loading'" data-testid="gp-summary">
    <p v-if="status === 'loading'" class="muted" role="status"><small>{{ t('guitarpro.compare.loading') }}</small></p>
    <p v-else-if="status === 'error'" class="field-error" role="alert"><small>{{ t('guitarpro.compare.failed') }}</small></p>
    <template v-else>
      <ul v-if="lines.length > 0" class="lines">
        <li v-for="(line, i) in lines" :key="i">{{ line }}</li>
      </ul>
      <p v-else class="muted"><small>{{ t('guitarpro.compare.summary.none') }}</small></p>
      <RouterLink class="tab-link" data-testid="gp-show-changes"
                  :aria-label="t('guitarpro.compare.summary.showLabel', { file: file.originalFileName })"
                  :to="{ name: 'tablature', params: { songId, versionId: b.id, fileId: file.id }, query: { a: a.id, aFile: fileA.id } }">
        {{ t('guitarpro.compare.summary.show') }}
      </RouterLink>
    </template>
  </div>
</template>

<style scoped>
.gp-summary { display: grid; gap: .35rem; justify-items: start; margin: .25rem 0 .5rem; color: var(--text); }
.gp-summary p { margin: 0; }
.lines { margin: 0; padding-left: 1.25rem; }
.tab-link { padding: .35rem .9rem; border: 1px solid var(--primary); border-radius: var(--radius); text-decoration: none; }
</style>
