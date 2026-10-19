<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { stemsApi } from '../api/stemsApi'
import type { Stem, StemInput } from '../types'
import { decodedBytes, deviceHints, maxSeconds, memoryBudget, BYTES_PER_SECOND } from '../budget'
import { useStemMixer } from '../composables/useStemMixer'
import StemMixer from './StemMixer.vue'
import StemsEditor from './StemsEditor.vue'
import { filesApi } from '@/features/files/api/filesApi'
import type { SongFile } from '@/features/files/types'
import SectionCard from '@/shared/components/SectionCard.vue'
import LoadingState from '@/shared/components/LoadingState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import { getErrorMessage } from '@/shared/api/ApiError'
import { useAsyncAction } from '@/shared/composables/useAsyncAction'
import { useToast } from '@/shared/composables/useToast'
import { formatTime } from '@/shared/utils/format'

/**
 * Pistes séparées d'une version (#23, ADR 0030). files est la liste de la page : quand un fichier arrive,
 * part à la corbeille ou finit son analyse et son aperçu, on recharge les pistes.
 */
const props = defineProps<{ songId: string; versionId: string; files: readonly SongFile[]; readonly: boolean }>()
const toast = useToast()

const stems = ref<Stem[]>([])
const isLoading = ref(true)
const loadError = ref<unknown>(null)
const editing = ref(false)

const mixer = useStemMixer((s) => filesApi.listenUrl(props.songId, props.versionId, s.fileId))
const playable = computed(() => stems.value.filter((s) => s.playable))
const waiting = computed(() => stems.value.filter((s) => !s.playable))
/** Ce que le mélangeur a chargé : s'il change, le mélangeur ne correspond plus aux pistes. */
let loadedKey = ''
const playableKey = computed(() => playable.value.map((s) => `${s.fileId}:${s.name}`).join('|'))

async function load() {
  loadError.value = null
  try {
    stems.value = await stemsApi.list(props.songId, props.versionId)
    if (mixer.state.value !== 'idle' && playableKey.value !== loadedKey) mixer.dispose()
  } catch (e) {
    loadError.value = e
  } finally {
    isLoading.value = false
  }
}
watch(() => props.files.map((f) => `${f.id}:${f.preview ?? ''}:${f.scan ?? ''}`).join('|'), load, { immediate: true })

// ---- Mémoire ----
const budget = memoryBudget(deviceHints())
const totalSeconds = computed(() => playable.value.reduce((sum, s) => sum + (s.durationMs ?? 0) / 1000, 0))
const fits = computed(() => decodedBytes(playable.value.map((s) => s.durationMs ?? 0)) <= budget)
const neededMb = computed(() => Math.ceil((totalSeconds.value * BYTES_PER_SECOND) / 1024 / 1024))

function onLoadMixer() {
  if (!fits.value || playable.value.length === 0) return
  loadedKey = playableKey.value
  void mixer.load(playable.value)
}

// ---- Choix des pistes ----
const saving = useAsyncAction((input: StemInput[]) => stemsApi.save(props.songId, props.versionId, input))

async function onSave(input: StemInput[]) {
  const saved = await saving.run(input)
  if (!saved) return
  stems.value = saved
  editing.value = false
  if (mixer.state.value !== 'idle' && playableKey.value !== loadedKey) mixer.dispose()
  toast.success(saved.length ? 'Pistes enregistrées.' : 'Mélangeur retiré.')
}
</script>

<template>
  <SectionCard title="Pistes séparées">
    <template v-if="!readonly && !editing && !isLoading && !loadError" #actions>
      <button type="button" class="secondary" @click="editing = true">
        {{ stems.length ? 'Modifier les pistes' : 'Choisir les pistes' }}
      </button>
    </template>

    <LoadingState v-if="isLoading" />
    <ErrorState v-else-if="loadError" :message="getErrorMessage(loadError)" @retry="load()" />

    <template v-else-if="editing">
      <StemsEditor :files="files" :stems="stems" :saving="saving.isLoading.value" @save="onSave" @cancel="editing = false" />
      <p v-if="saving.error.value" class="field-error" role="alert">{{ getErrorMessage(saving.error.value) }}</p>
    </template>

    <p v-else-if="stems.length === 0" class="muted">
      <template v-if="readonly">Pas de pistes séparées pour cette version.</template>
      <template v-else>
        Envoyez un fichier audio par instrument (voix, guitare, batterie…), puis choisissez-les ici pour les écouter
        ensemble et régler chacun.
      </template>
    </p>

    <template v-else>
      <p v-if="waiting.length" class="muted" role="status" data-testid="stems-waiting">
        Pas encore jouables (analyse ou aperçu en cours, ou en échec dans Fichiers) : {{ waiting.map((s) => s.name).join(', ') }}.
      </p>

      <StemMixer v-if="mixer.state.value === 'ready' && mixer.engine.value" :engine="mixer.engine.value"
                 :names="playable.map((s) => s.name)" />

      <template v-else>
        <ul class="stem-list">
          <li v-for="s in stems" :key="s.fileId">{{ s.name }} <small class="muted">· {{ s.originalFileName }}</small></li>
        </ul>
        <p v-if="!fits" class="field-error" role="alert" data-testid="stems-too-long">
          Trop long pour la mémoire de cet appareil : {{ formatTime(totalSeconds) }} de pistes au total
          (environ {{ neededMb }} Mo une fois décodées), {{ formatTime(maxSeconds(budget)) }} au plus ici.
          Retirez une piste ou écoutez les fichiers un par un.
        </p>
        <p v-if="mixer.state.value === 'error'" class="field-error" role="alert">{{ mixer.error.value }}</p>
        <button type="button" :disabled="!fits || playable.length === 0 || mixer.state.value === 'loading'" @click="onLoadMixer">
          <template v-if="mixer.state.value === 'loading'">Chargement… {{ mixer.loaded.value }}/{{ playable.length }}</template>
          <template v-else>Ouvrir le mélangeur ({{ playable.length }} {{ playable.length > 1 ? 'pistes' : 'piste' }})</template>
        </button>
      </template>
    </template>
  </SectionCard>
</template>

<style scoped>
.stem-list { margin: 0 0 .75rem; padding-left: 1.25rem; }
</style>