<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { versionsApi } from '../api/versionsApi'
import type { SongVersion, SongVersionSummary } from '../types'
import { defaultPair, firstAudio, parseVersionNumber } from '../compare'
import StatusBadge from '../components/StatusBadge.vue'
import { songsApi } from '@/features/songs/api/songsApi'
import { usePlayerStore } from '@/features/player/stores/playerStore'
import { toTrack, type Track } from '@/features/player/track'
import LoadingState from '@/shared/components/LoadingState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import { getErrorMessage } from '@/shared/api/ApiError'
import { formatDate } from '@/shared/utils/format'

type Side = 'a' | 'b'

const props = defineProps<{ songId: string }>()
const route = useRoute()
const router = useRouter()
const player = usePlayerStore()

const songTitle = ref('')
const versions = ref<SongVersionSummary[]>([])
const sides = ref<Record<Side, SongVersion | null>>({ a: null, b: null })
const isLoading = ref(true)
const error = ref<unknown>(null)

// ---- Sélection : l'URL fait foi, sinon les deux plus récentes ----
const selected = computed<Record<Side, number | null>>(() => {
  const fallback = defaultPair(versions.value.map((v) => v.number))
  return {
    a: parseVersionNumber(route.query.a) ?? fallback?.[0] ?? null,
    b: parseVersionNumber(route.query.b) ?? fallback?.[1] ?? null,
  }
})

function choose(side: Side, value: string) {
  void router.replace({ query: { ...route.query, [side]: value } })
}

// ---- Chargement ----
async function loadSides() {
  const idOf = (n: number | null) => versions.value.find((v) => v.number === n)?.id
  const idA = idOf(selected.value.a)
  const idB = idOf(selected.value.b)
  if (!idA || !idB) {
    sides.value = { a: null, b: null }
    return
  }
  try {
    const [a, b] = await Promise.all([versionsApi.get(props.songId, idA), versionsApi.get(props.songId, idB)])
    sides.value = { a, b }
  } catch (e) {
    error.value = e
  }
}

async function load() {
  isLoading.value = true
  error.value = null
  try {
    const [song, list] = await Promise.all([songsApi.get(props.songId), versionsApi.list(props.songId)])
    songTitle.value = song.title
    versions.value = list
    await loadSides()
  } catch (e) {
    error.value = e
  } finally {
    isLoading.value = false
  }
}

watch(() => props.songId, load, { immediate: true })
watch(() => [selected.value.a, selected.value.b], () => loadSides())

// ---- Écoute A/B ----
function trackFor(side: Side): Track | null {
  const version = sides.value[side]
  const file = version ? firstAudio(version.files) : null
  if (!version || !file) return null
  return toTrack({ songId: props.songId, songTitle: songTitle.value, versionId: version.id, versionNumber: version.number, file })
}

const activeSide = computed<Side | null>(() => {
  const src = player.track?.src
  if (!src) return null
  if (trackFor('a')?.src === src) return 'a'
  if (trackFor('b')?.src === src) return 'b'
  return null
})

async function listen(side: Side) {
  const track = trackFor(side)
  if (!track) return
  // Déjà en train d'écouter l'autre côté → bascule en gardant la position
  if (activeSide.value && activeSide.value !== side) await player.switchTo(track)
  else await player.play(track)
}
</script>

<template>
  <section>
    <RouterLink :to="{ name: 'song-detail', params: { songId } }">← {{ songTitle || 'Retour au morceau' }}</RouterLink>
    <h1>Comparer deux versions</h1>

    <LoadingState v-if="isLoading" />
    <ErrorState v-else-if="error" :message="getErrorMessage(error)" @retry="load()" />
    <p v-else-if="versions.length < 2" class="card">Il faut au moins deux versions pour comparer.</p>

    <template v-else>
      <div class="ab-bar card">
        <button v-for="side in (['a', 'b'] as const)" :key="side" type="button"
                :class="{ secondary: activeSide !== side }" :aria-pressed="activeSide === side"
                :disabled="!trackFor(side)" @click="listen(side)">
          Écouter {{ side.toUpperCase() }}
        </button>
        <small class="muted">La bascule conserve la position : pertinent si les deux maquettes ont le même tempo.</small>
      </div>

      <div class="columns">
        <article v-for="side in (['a', 'b'] as const)" :key="side" class="card column">
          <label :for="`select-${side}`"><strong>{{ side.toUpperCase() }}</strong></label>
          <select :id="`select-${side}`" :value="selected[side] ?? ''"
                  @change="choose(side, ($event.target as HTMLSelectElement).value)">
            <option v-for="v in versions" :key="v.id" :value="v.number">v{{ v.number }} — {{ v.title }}</option>
          </select>

          <template v-if="sides[side]">
            <p><StatusBadge :status="sides[side]!.status" /></p>
            <p>
              <strong>BPM :</strong> {{ sides[side]!.bpm ?? '—' }}
              · <strong>Tonalité :</strong> {{ sides[side]!.key ?? '—' }}
            </p>
            <p class="muted"><small>Créée le {{ formatDate(sides[side]!.createdAt) }}</small></p>
            <h3>Notes</h3>
            <p class="pre">{{ sides[side]!.notes ?? '—' }}</p>
            <h3>Paroles</h3>
            <p class="pre">{{ sides[side]!.lyrics ?? '—' }}</p>   <!-- interpolation + pre-wrap, jamais v-html -->
            <h3>Fichiers</h3>
            <ul>
              <li v-for="f in sides[side]!.files" :key="f.id">{{ f.originalFileName }}</li>
            </ul>
            <p v-if="!firstAudio(sides[side]!.files)" class="muted">Aucun fichier audio.</p>
          </template>
        </article>
      </div>
    </template>
  </section>
</template>

<style scoped>
.ab-bar { display: flex; gap: .75rem; align-items: center; flex-wrap: wrap; margin-bottom: 1rem; }
.columns { display: grid; grid-template-columns: 1fr 1fr; gap: 1rem; }
.column { display: grid; gap: .5rem; align-content: start; }
@media (max-width: 700px) { .columns { grid-template-columns: 1fr; } }
</style>
