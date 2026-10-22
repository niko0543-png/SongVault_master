<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, shallowRef, watch } from 'vue'
import { AlphaTabApi, LayoutMode, PlayerMode, synth, type model } from '@coderline/alphatab'
import { usePlayerStore } from '@/features/player/stores/playerStore'
import { t } from '@/shared/i18n'

/**
 * #28 : partition et tablature d'un fichier Guitar Pro, jouées dans le navigateur par alphaTab (ADR 0034).
 * Ce composant est chargé à la demande (FileList) : alphaTab n'est téléchargé qu'à l'ouverture d'une tablature.
 * Lecture seule : tempo, boucle, pistes et transposition ne changent que l'écoute, jamais le fichier.
 * #30 : la comparaison lui donne une partition déjà lue et colorée (`score`), la piste à montrer (`track`),
 * et le fait se placer sur une mesure (`goToBar`).
 */
const props = defineProps<{ src?: string; score?: model.Score | null; track?: number; label: string }>()
const player = usePlayerStore()

const SPEEDS = [0.5, 0.75, 0.9, 1] as const
const MAX_TRANSPOSE = 12

const host = ref<HTMLElement | null>(null)
const status = ref<'loading' | 'ready' | 'error'>('loading')
const canPlay = ref(false)
const isPlaying = ref(false)
const speed = ref<number>(1)
const loop = ref(false)
const transpose = ref(0)
const tracks = shallowRef<model.Track[]>([])
const shown = ref<number[]>([0])
const mixer = reactive<{ mute: boolean[]; solo: boolean[] }>({ mute: [], solo: [] })
let api: AlphaTabApi | null = null

const transposeText = computed(() =>
  t('guitarpro.viewer.semitones', { count: transpose.value > 0 ? `+${transpose.value}` : String(transpose.value) }))

onMounted(async () => {
  api = new AlphaTabApi(host.value!, {
    core: { fontDirectory: '/font/' },                       // copiés à la racine du site par le plugin Vite
    display: { layoutMode: LayoutMode.Page },
    player: { playerMode: PlayerMode.EnabledSynthesizer, soundFont: '/soundfont/sonivox.sf2' },
  })
  api.scoreLoaded.on((score) => {
    tracks.value = score.tracks
    mixer.mute = score.tracks.map(() => false)
    mixer.solo = score.tracks.map(() => false)
  })
  api.renderFinished.on(() => { status.value = 'ready' })
  api.playerReady.on(() => { canPlay.value = true })
  api.playerStateChanged.on((e) => {
    isPlaying.value = e.state === synth.PlayerState.Playing
    if (isPlaying.value) player.pause()                       // un seul son à la fois : le lecteur du bas se tait
  })
  api.error.on(() => { status.value = 'error' })
  if (props.score) {                                        // #30 : partition déjà lue par la comparaison
    shown.value = [props.track ?? 0]
    api.renderScore(props.score, shown.value)
    return
  }
  try {
    const response = await fetch(props.src!)                   // suit la redirection vers le stockage (ADR 0012)
    if (!response.ok) throw new Error(String(response.status))
    const bytes = new Uint8Array(await response.arrayBuffer())
    if (!api?.load(bytes, [0])) throw new Error('format')
  } catch {
    status.value = 'error'
  }
})

onBeforeUnmount(() => {
  api?.destroy()
  api = null
})

// Le lecteur du bas repart (autre fichier écouté) : la tablature se met en pause
watch(() => player.isPlaying, (playing) => { if (playing && isPlaying.value) api?.pause() })

// #30 : la comparaison change de piste
watch(() => props.track, (index) => {
  const track = index === undefined ? undefined : props.score?.tracks[index]
  if (!api || !track) return
  shown.value = [index!]
  api.renderTracks([track])
})

watch(speed, (value) => { if (api) api.playbackSpeed = value })
watch(loop, (value) => { if (api) api.isLooping = value })

function playPause() {
  api?.playPause()
}

function stop() {
  api?.stop()
}

function step(delta: number) {
  transpose.value = Math.max(-MAX_TRANSPOSE, Math.min(MAX_TRANSPOSE, transpose.value + delta))
  if (!api?.score) return
  api.changeTrackTranspositionPitch(api.score.tracks, transpose.value)              // le son
  api.settings.notation.transpositionPitches = api.score.tracks.map(() => transpose.value)   // la portée
  api.updateSettings()
  api.render()
}

function toggleShown(index: number) {
  const next = shown.value.includes(index) ? shown.value.filter((i) => i !== index) : [...shown.value, index].sort((a, b) => a - b)
  if (next.length === 0 || !api) return                     // au moins une piste affichée
  shown.value = next
  api.renderTracks(next.map((i) => tracks.value[i]!))
}

function toggleMute(index: number) {
  mixer.mute[index] = !mixer.mute[index]
  api?.changeTrackMute([tracks.value[index]!], mixer.mute[index]!)
}

function toggleSolo(index: number) {
  mixer.solo[index] = !mixer.solo[index]
  api?.changeTrackSolo([tracks.value[index]!], mixer.solo[index]!)
}

/**
 * #30 : place la partition sur une mesure (0 = la première) : la page défile jusqu'à elle, la mesure est
 * sélectionnée (« Boucler la sélection » la rejoue) et la lecture partira de là.
 */
function goToBar(index: number) {
  const score = api?.score
  const master = score?.masterBars[Math.max(0, Math.min(index, score.masterBars.length - 1))]
  if (!api || !master) return
  const beats = api.tracks[0]?.staves[0]?.bars[master.index]?.voices[0]?.beats ?? []
  if (beats.length > 0) api.highlightPlaybackRange(beats[0]!, beats[beats.length - 1]!)
  api.tickPosition = master.start
  const bounds = api.boundsLookup?.findMasterBarByIndex(master.index)
  if (bounds && host.value) {
    const top = host.value.getBoundingClientRect().top + window.scrollY + bounds.realBounds.y - 16
    window.scrollTo({ top, behavior: 'smooth' })
  }
}

defineExpose({ goToBar })
</script>

<template>
  <section class="tab-viewer" :aria-label="t('guitarpro.viewer.label', { label })" :aria-busy="status === 'loading'">
    <p v-if="status === 'loading'" class="muted" role="status">{{ t('guitarpro.viewer.loading') }}</p>
    <p v-else-if="status === 'error'" class="field-error" role="alert">{{ t('guitarpro.viewer.loadFailed') }}</p>

    <template v-if="status === 'ready'">
      <div class="toolbar">
        <button type="button" class="play" data-testid="tab-play" :disabled="!canPlay"
                :aria-label="isPlaying ? t('guitarpro.viewer.pause') : t('guitarpro.viewer.play')" @click="playPause">
          {{ isPlaying ? '⏸' : '▶' }}
        </button>
        <button type="button" class="secondary" :disabled="!canPlay" @click="stop">{{ t('guitarpro.viewer.stop') }}</button>
        <span v-if="!canPlay" class="muted" role="status">{{ t('guitarpro.viewer.soundLoading') }}</span>
        <label class="inline">
          {{ t('guitarpro.viewer.tempo') }}
          <select v-model.number="speed">
            <option v-for="s in SPEEDS" :key="s" :value="s">{{ Math.round(s * 100) }} %</option>
          </select>
        </label>
        <label class="inline"><input v-model="loop" type="checkbox"> {{ t('guitarpro.viewer.loop') }}</label>
        <span class="inline" role="group" :aria-label="t('guitarpro.viewer.transpose')">
          <button type="button" class="secondary" :aria-label="t('guitarpro.viewer.transposeDown')"
                  :disabled="transpose <= -MAX_TRANSPOSE" @click="step(-1)">−</button>
          <output data-testid="tab-transpose">{{ transposeText }}</output>
          <button type="button" class="secondary" :aria-label="t('guitarpro.viewer.transposeUp')"
                  :disabled="transpose >= MAX_TRANSPOSE" @click="step(1)">+</button>
        </span>
      </div>

      <fieldset v-if="tracks.length > 1 && !score" class="tracks">
        <legend>{{ t('guitarpro.viewer.tracks') }}</legend>
        <div v-for="(track, i) in tracks" :key="i" class="track">
          <label class="inline">
            <input type="checkbox" :checked="shown.includes(i)" :aria-label="t('guitarpro.viewer.show', { name: track.name })"
                   @change="toggleShown(i)">
            {{ track.name }}
          </label>
          <button type="button" class="secondary" :aria-pressed="mixer.mute[i]"
                  :aria-label="t('guitarpro.viewer.muteTrack', { name: track.name })" @click="toggleMute(i)">{{ t('guitarpro.viewer.mute') }}</button>
          <button type="button" class="secondary" :aria-pressed="mixer.solo[i]"
                  :aria-label="t('guitarpro.viewer.soloTrack', { name: track.name })" @click="toggleSolo(i)">{{ t('guitarpro.viewer.solo') }}</button>
        </div>
      </fieldset>

      <p class="muted hint"><small>{{ t('guitarpro.viewer.loopHint') }} {{ t('guitarpro.viewer.readOnly') }}</small></p>
    </template>

    <!-- alphaTab dessine ici ; caché tant que la partition n'est pas prête, mais présent pour mesurer la largeur -->
    <div ref="host" class="score" :class="{ pending: status !== 'ready' }" data-testid="tab-score" />
  </section>
</template>

<style scoped>
.tab-viewer { display: grid; gap: .5rem; min-width: 0; }
.toolbar { display: flex; flex-wrap: wrap; gap: .5rem; align-items: center; }
.inline { display: inline-flex; gap: .35rem; align-items: center; width: auto; }
.inline select { width: auto; }
.play { min-width: 44px; min-height: 44px; }
.tracks { display: grid; gap: .25rem; }
.track { display: flex; flex-wrap: wrap; gap: .5rem; align-items: center; }
.track button[aria-pressed='true'] { background: var(--primary); color: #fff; }
.hint { margin: 0; }
/* #14 : la partition suit la largeur, sans défilement horizontal à 360 px */
.score { width: 100%; max-width: 100%; overflow: hidden; background: #fff; color: #000; border-radius: 4px; }
.score.pending { height: 0; }
/* Curseur de lecture et sélection, dessinés par alphaTab hors du gabarit de Vue */
.score :deep(.at-cursor-bar) { background: rgb(255 242 0 / 25%); }
.score :deep(.at-selection div) { background: rgb(64 64 255 / 10%); }
.score :deep(.at-cursor-beat) { background: rgb(64 64 255 / 75%); width: 3px; }
.score :deep(.at-highlight *) { fill: #0078ff; stroke: #0078ff; }
</style>