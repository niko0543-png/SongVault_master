<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, shallowRef, watch } from 'vue'
import { AlphaTabApi, PlayerMode, synth, type model } from '@coderline/alphatab'
import { usePlayerStore } from '@/features/player/stores/playerStore'
import { filesApi } from '@/features/files/api/filesApi'
import { getErrorMessage } from '@/shared/api/ApiError'
import { useConfirm } from '@/shared/composables/useConfirm'
import TabSyncRecorder from './TabSyncRecorder.vue'
import TabTrackPanel from './TabTrackPanel.vue'
import { syncApi } from '../sync/syncApi'
import { playOrder, pointsFromTaps } from '../sync/syncPoints'
import { connectExternalMedia } from '../sync/externalMedia'
import type { PlayedBar, SyncContext, TabSync } from '../sync/types'
import { GUITAR_PRO_RESOURCES, STAFF_VIEWS, WIDE_SCREEN, ZOOMS, applyStaffView, fileStaves, layoutFor, type StaffView } from '../display'
import { isGlobalShortcut } from '@/shared/utils/keyboard'
import { t } from '@/shared/i18n'

/**
 * #28 : partition et tablature d'un fichier Guitar Pro, jouées dans le navigateur par alphaTab (ADR 0034).
 * Ce composant est chargé à la demande (FileList) : alphaTab n'est téléchargé qu'à l'ouverture d'une tablature.
 * Lecture seule : tempo, boucle, pistes et transposition ne changent que l'écoute, jamais le fichier.
 * #30 : la comparaison lui donne une partition déjà lue et colorée (`score`), la piste à montrer (`track`),
 * et le fait se placer sur une mesure (`goToBar`).
 * #29 : avec `sync` (FileList), la tablature peut suivre une maquette de la même version au lieu de son propre son :
 * le curseur suit alors l'enregistrement, grâce aux points de synchronisation enregistrés (ADR 0036).
 * #31 : rendu proche de Guitar Pro : mise en page du fichier sur grand écran, page blanche, zoom, portées,
 * métronome et décompte, et le panneau des pistes collé en bas de l'écran (TabTrackPanel) (ADR 0037).
 * #32 : avec `fill` (TablaturePage), le lecteur occupe tout son parent, en un seul bloc : commandes en haut,
 * partition qui défile seule dans son cadre, pistes en bas ; et le clavier le pilote (ADR 0038).
 */
const props = defineProps<{
  src?: string; score?: model.Score | null; track?: number; label: string; sync?: SyncContext; fill?: boolean
}>()
const player = usePlayerStore()
const { confirm } = useConfirm()

/** #32 : tempo de 25 à 150 %, par pas de 5 % (alphaTab : 1 = celui du fichier). */
const SPEED_MIN = 0.25
const SPEED_MAX = 1.5
const SPEED_STEP = 0.05
const MAX_TRANSPOSE = 12

const host = ref<HTMLElement | null>(null)
const desk = ref<HTMLElement | null>(null)
const status = ref<'loading' | 'ready' | 'error'>('loading')
const playerReady = ref(false)
const isPlaying = ref(false)
const speed = ref<number>(1)
const loop = ref(false)
const transpose = ref(0)
const tracks = shallowRef<model.Track[]>([])
const shown = ref<number[]>([0])
const mixer = reactive<{ mute: boolean[]; solo: boolean[]; volume: number[] }>({ mute: [], solo: [], volume: [] })
let api: AlphaTabApi | null = null

// #31 : affichage façon Guitar Pro
const zoom = ref(1)
const staffView = ref<StaffView>('file')
const metronome = ref(false)
const countIn = ref(false)
let staves = new Map<model.Staff, { score: boolean; tab: boolean }>()
const wideScreen = typeof window.matchMedia === 'function' ? window.matchMedia(WIDE_SCREEN) : null
const zoomText = (z: number) => `${Math.round(z * 100)} %`

// #29 : la mesure jouée, et le calage sur une maquette
const barCount = ref(0)
const currentBar = ref(1)
const saved = ref<TabSync | null>(null)
const source = ref<'synth' | 'audio'>('synth')
const recording = ref(false)
const saving = ref(false)
const syncError = ref<string | null>(null)
const order = shallowRef<PlayedBar[]>([])
const media = ref<HTMLAudioElement | null>(null)
/** La maquette connaît sa durée : avant, alphaTab croirait le morceau fini dès le premier instant. */
const mediaReady = ref(false)
let disconnect: (() => void) | null = null
let sourceChosen = false
const savedAudio = computed(() => props.sync?.audios.find((a) => a.id === saved.value?.audioFileId) ?? null)
const followsAudio = computed(() => source.value === 'audio')
const canPlay = computed(() => playerReady.value && (!followsAudio.value || mediaReady.value))
const listenUrl = (audioFileId: string) => filesApi.listenUrl(props.sync!.songId, props.sync!.versionId, audioFileId)

const speedText = computed(() => `${Math.round(speed.value * 100)} %`)
const transposeText = computed(() =>
  t('guitarpro.viewer.semitones', { count: transpose.value > 0 ? `+${transpose.value}` : String(transpose.value) }))

onMounted(async () => {
  api = new AlphaTabApi(host.value!, {
    core: { fontDirectory: '/font/' },                       // copiés à la racine du site par le plugin Vite
    display: { layoutMode: layoutFor(wideScreen?.matches ?? true), resources: GUITAR_PRO_RESOURCES },
    player: {
      playerMode: PlayerMode.EnabledSynthesizer, soundFont: '/soundfont/sonivox.sf2',
      // #32 : alphaTab retient l'élément qui défile à sa création : en plein cadre, c'est la partition, pas la page
      ...(props.fill ? { scrollElement: desk.value! } : {}),
    },
  })
  api.scoreLoaded.on((score) => {
    tracks.value = score.tracks
    mixer.mute = score.tracks.map(() => false)
    mixer.solo = score.tracks.map(() => false)
    mixer.volume = score.tracks.map(() => 1)
    barCount.value = score.masterBars.length
    staves = fileStaves(score)
    staffView.value = 'file'
  })
  wideScreen?.addEventListener('change', relayout)
  if (props.fill) window.addEventListener('keydown', onKeydown, { capture: true })
  api.playedBeatChanged.on((beat) => { currentBar.value = beat.voice.bar.index + 1 })
  api.renderFinished.on(() => { status.value = 'ready' })
  api.playerReady.on(() => { playerReady.value = true })
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
  void loadSync()
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
  wideScreen?.removeEventListener('change', relayout)
  window.removeEventListener('keydown', onKeydown, { capture: true })
  disconnect?.()
  api?.destroy()
  api = null
})

async function loadSync() {
  const sync = props.sync
  if (!sync) return
  try {
    saved.value = (await syncApi.get(sync.songId, sync.versionId, sync.fileId)) ?? null
  } catch (e) {
    syncError.value = getErrorMessage(e)                     // la tablature se joue quand même, avec son propre son
  }
}

// #29 : une tablature calée suit sa maquette dès l'ouverture ; « Son » permet de revenir au son de la tablature.
// #31 : seulement une fois les sons chargés (playerReady) : sinon la banque de sons, arrivée après le changement
// de lecteur, met la maquette en pause dès son départ (vu dans Playwright avec la mise en page du fichier).
watch([savedAudio, status, playerReady], () => {
  if (sourceChosen || status.value !== 'ready' || !playerReady.value || !savedAudio.value) return
  sourceChosen = true
  source.value = 'audio'
})
// Après le rendu : l'élément <audio> de la maquette doit exister avant d'être branché
watch(source, () => applySource(), { flush: 'post' })

/** Change le lecteur d'alphaTab : son propre son (synthétiseur), ou la maquette, suivie par les points du calage. */
function applySource() {
  const score = api?.score
  if (!api || !score) return
  api.stop()
  disconnect?.()
  disconnect = null
  playerReady.value = false
  const sync = followsAudio.value ? saved.value : null
  score.applyFlatSyncPoints(sync?.points ?? [])
  api.settings.player.playerMode = sync ? PlayerMode.EnabledExternalMedia : PlayerMode.EnabledSynthesizer
  api.updateSettings()
  if (sync && media.value) disconnect = connectExternalMedia(api, media.value)
  api.loadMidiForScore()
}

function startRecording() {
  api?.pause()
  syncError.value = null
  order.value = playOrder(api?.tickCache?.masterBars ?? [])
  recording.value = order.value.length > 0
}

/** Le calage montre la mesure à taper : sélectionnée, et la page défile jusqu'à elle. */
function showNext(index: number) {
  const bar = order.value[index]
  if (bar) goToBar(bar.barIndex)
}

function stopRecording() {
  recording.value = false
  if (api) api.playbackRange = null                         // la sélection posée pendant le calage
}

async function saveSync(audioFileId: string, tapsMs: number[]) {
  const sync = props.sync
  if (!sync) return
  saving.value = true
  syncError.value = null
  try {
    saved.value = await syncApi.save(sync.songId, sync.versionId, sync.fileId, audioFileId, pointsFromTaps(order.value, tapsMs))
    stopRecording()
    sourceChosen = true
    if (followsAudio.value) applySource()                    // déjà sur la maquette : les nouveaux points s'appliquent
    else source.value = 'audio'
  } catch (e) {
    syncError.value = getErrorMessage(e)
  } finally {
    saving.value = false
  }
}

async function removeSync() {
  const sync = props.sync
  if (!sync) return
  const confirmed = await confirm({
    title: t('guitarpro.sync.removeTitle'),
    message: t('guitarpro.sync.removeMessage'),
    confirmLabel: t('guitarpro.sync.remove'),
    danger: true,
  })
  if (!confirmed) return
  syncError.value = null
  try {
    await syncApi.remove(sync.songId, sync.versionId, sync.fileId)
    saved.value = null
    source.value = 'synth'
  } catch (e) {
    syncError.value = getErrorMessage(e)
  }
}

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

// #31 : zoom, portées, métronome et décompte ; seul l'affichage ou l'écoute change, jamais le fichier
watch(zoom, (value) => {
  if (!api) return
  api.settings.display.scale = value
  api.updateSettings()
  api.render()
})
watch(staffView, (value, previous) => {
  if (!api?.score || value === previous) return
  applyStaffView(api.score, value, staves)
  api.render()
})
watch(metronome, (on) => { if (api) api.metronomeVolume = on ? 1 : 0 })
watch(countIn, (on) => { if (api) api.countInVolume = on ? 1 : 0 })

/** La fenêtre passe sous 720 px ou au-dessus : mise en page du fichier, ou rangement automatique. */
function relayout() {
  if (!api) return
  api.settings.display.layoutMode = layoutFor(wideScreen?.matches ?? true)
  api.updateSettings()
  api.render()
}

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

/** #31 : volume d'une piste, de 0 à 2 fois celui du fichier (alphaTab : 1 = tel quel). */
function setVolume(index: number, value: number) {
  mixer.volume[index] = value
  api?.changeTrackVolume([tracks.value[index]!], value)
}

/**
 * #30 : place la partition sur une mesure (0 = la première) : la page défile jusqu'à elle, la mesure est
 * sélectionnée (« Boucler la sélection » la rejoue) et la lecture partira de là.
 * #32 : `select: false` (flèches du clavier) déplace seulement ; en plein cadre, c'est la partition qui défile.
 */
function goToBar(index: number, select = true) {
  const score = api?.score
  const master = score?.masterBars[Math.max(0, Math.min(index, score.masterBars.length - 1))]
  if (!api || !master) return
  const beats = api.tracks[0]?.staves[0]?.bars[master.index]?.voices[0]?.beats ?? []
  if (select && beats.length > 0) api.highlightPlaybackRange(beats[0]!, beats[beats.length - 1]!)
  api.tickPosition = master.start
  currentBar.value = master.index + 1                       // à l'arrêt, alphaTab ne signale pas le déplacement
  const bounds = api.boundsLookup?.findMasterBarByIndex(master.index)
  if (!bounds || !host.value) return
  const frame = props.fill ? desk.value : null
  if (frame) {
    const top = frame.scrollTop + host.value.getBoundingClientRect().top - frame.getBoundingClientRect().top + bounds.realBounds.y - 16
    frame.scrollTo({ top, behavior: 'smooth' })
  } else {
    const top = host.value.getBoundingClientRect().top + window.scrollY + bounds.realBounds.y - 16
    window.scrollTo({ top, behavior: 'smooth' })
  }
}

/**
 * #32 : raccourcis de la page du lecteur, comme dans Guitar Pro : Espace lecture ou pause, ← → mesure précédente
 * ou suivante, L boucle, Échap retour au début. Écoutés avant le lecteur du bas (capture), qui ne réagit donc pas
 * à Espace ici ; jamais pendant la saisie ni sur un bouton ou un champ qui a le focus (isGlobalShortcut).
 */
function onKeydown(event: KeyboardEvent) {
  if (status.value !== 'ready' || recording.value || event.defaultPrevented || !isGlobalShortcut(event)) return
  const action = SHORTCUTS[event.key === ' ' ? 'Space' : event.key.length === 1 ? event.key.toLowerCase() : event.key]
  if (!action) return
  event.preventDefault()                                      // Espace et flèches ne font pas défiler
  event.stopPropagation()
  action()
}

const SHORTCUTS: Record<string, () => void> = {
  Space: () => { if (canPlay.value) playPause() },
  Escape: () => { if (canPlay.value) stop() },
  ArrowLeft: () => goToBar(currentBar.value - 2, false),
  ArrowRight: () => goToBar(currentBar.value, false),
  l: () => { loop.value = !loop.value },
}

defineExpose({ goToBar })
</script>

<template>
  <section class="tab-viewer" :class="{ fill }" :aria-label="t('guitarpro.viewer.label', { label })" :aria-busy="status === 'loading'">
    <p v-if="status === 'loading'" class="muted" role="status">{{ t('guitarpro.viewer.loading') }}</p>
    <p v-else-if="status === 'error'" class="field-error" role="alert">{{ t('guitarpro.viewer.loadFailed') }}</p>

    <template v-if="status === 'ready'">
      <!-- #32 : en plein cadre, les commandes restent au-dessus de la partition, qui défile seule -->
      <div class="controls">
      <div class="toolbar">
        <button type="button" class="play" data-testid="tab-play" :disabled="!canPlay" :aria-keyshortcuts="fill ? 'Space' : undefined"
                :aria-label="isPlaying ? t('guitarpro.viewer.pause') : t('guitarpro.viewer.play')" @click="playPause">
          {{ isPlaying ? '⏸' : '▶' }}
        </button>
        <button type="button" class="secondary" data-testid="tab-stop" :disabled="!canPlay"
                :aria-keyshortcuts="fill ? 'Escape' : undefined" @click="stop">{{ t('guitarpro.viewer.stop') }}</button>
        <span v-if="!canPlay" class="muted" role="status">{{ t('guitarpro.viewer.soundLoading') }}</span>
        <label class="inline">
          {{ t('guitarpro.viewer.tempo') }}
          <input v-model.number="speed" class="tempo" type="range" :min="SPEED_MIN" :max="SPEED_MAX" :step="SPEED_STEP"
                 data-testid="tab-tempo" :aria-valuetext="speedText">
          <output data-testid="tab-tempo-value">{{ speedText }}</output>
        </label>
        <button v-if="speed !== 1" type="button" class="secondary" data-testid="tab-tempo-reset" @click="speed = 1">
          {{ t('guitarpro.viewer.tempoReset') }}
        </button>
        <label class="inline"><input v-model="loop" type="checkbox" data-testid="tab-loop" :aria-keyshortcuts="fill ? 'L' : undefined"> {{ t('guitarpro.viewer.loop') }}</label>
        <span class="inline" role="group" :aria-label="t('guitarpro.viewer.transpose')">
          <button type="button" class="secondary" :aria-label="t('guitarpro.viewer.transposeDown')"
                  :disabled="followsAudio || transpose <= -MAX_TRANSPOSE" @click="step(-1)">−</button>
          <output data-testid="tab-transpose">{{ transposeText }}</output>
          <button type="button" class="secondary" :aria-label="t('guitarpro.viewer.transposeUp')"
                  :disabled="followsAudio || transpose >= MAX_TRANSPOSE" @click="step(1)">+</button>
        </span>
        <output class="muted" data-testid="tab-bar">{{ t('guitarpro.viewer.bar', { bar: currentBar, total: barCount }) }}</output>
      </div>

      <!-- #31 : réglages d'affichage, comme la barre de Guitar Pro -->
      <div class="toolbar view" data-testid="tab-view">
        <label class="inline">
          {{ t('guitarpro.viewer.zoom') }}
          <select v-model.number="zoom" data-testid="tab-zoom">
            <option v-for="z in ZOOMS" :key="z" :value="z">{{ zoomText(z) }}</option>
          </select>
        </label>
        <label class="inline">
          {{ t('guitarpro.viewer.staves') }}
          <select v-model="staffView" data-testid="tab-staves">
            <option v-for="v in STAFF_VIEWS" :key="v" :value="v">{{ t(`guitarpro.viewer.staffView.${v}`) }}</option>
          </select>
        </label>
        <label class="inline"><input v-model="metronome" type="checkbox" data-testid="tab-metronome" :disabled="followsAudio"> {{ t('guitarpro.viewer.metronome') }}</label>
        <label class="inline"><input v-model="countIn" type="checkbox" data-testid="tab-count-in" :disabled="followsAudio"> {{ t('guitarpro.viewer.countIn') }}</label>
      </div>

      <!-- #29 : suivre une maquette de la version, et la caler (Member ou Owner) -->
      <div v-if="sync && !score" class="sync" data-testid="tab-sync">
        <label v-if="savedAudio" class="inline">
          {{ t('guitarpro.sync.source') }}
          <select v-model="source" data-testid="tab-source" :disabled="recording">
            <option value="synth">{{ t('guitarpro.sync.synth') }}</option>
            <option value="audio">{{ t('guitarpro.sync.followAudio', { name: savedAudio.name }) }}</option>
          </select>
        </label>
        <template v-if="sync.canEdit && sync.audios.length > 0 && !recording">
          <button type="button" class="secondary" data-testid="sync-start" :disabled="!canPlay" @click="startRecording">
            {{ saved ? t('guitarpro.sync.redo') : t('guitarpro.sync.start') }}
          </button>
          <button v-if="saved" type="button" class="secondary" data-testid="sync-remove" @click="removeSync">{{ t('guitarpro.sync.remove') }}</button>
        </template>
        <small v-if="followsAudio" class="muted">{{ t('guitarpro.sync.audioHint') }}</small>
        <p v-if="syncError" class="field-error" role="alert">{{ syncError }}</p>
      </div>
      <TabSyncRecorder v-if="recording && sync" :audios="sync.audios" :initial-audio-id="saved?.audioFileId" :src="listenUrl"
                       :bars="order.length" :saving="saving" @next="showNext" @save="saveSync" @cancel="stopRecording" />

      <p class="muted hint">
        <small>{{ t('guitarpro.viewer.loopHint') }} <template v-if="fill">{{ t('guitarpro.viewer.shortcuts') }} </template>{{ t('guitarpro.viewer.readOnly') }}</small>
      </p>
      </div>
    </template>

    <!-- #29 : la maquette suivie ; alphaTab la commande (lecture, pause, déplacement, tempo) -->
    <audio v-if="savedAudio" ref="media" :src="listenUrl(savedAudio.id)" preload="auto" data-testid="tab-audio"
           @loadedmetadata="mediaReady = true" @emptied="mediaReady = false" />

    <!-- alphaTab dessine ici ; caché tant que la partition n'est pas prête, mais présent pour mesurer la largeur -->
    <!-- #31 : une page blanche sur fond gris, comme dans Guitar Pro -->
    <div ref="desk" class="desk" :class="{ pending: status !== 'ready' }" data-testid="tab-desk">
      <div ref="host" class="score" data-testid="tab-score" />
    </div>
     <!-- #31 : les pistes, collées en bas de l'écran pendant que la partition défile -->
    <TabTrackPanel v-if="status === 'ready' && tracks.length > 1 && !score" :tracks="tracks" :shown="shown"
                   :mute="mixer.mute" :solo="mixer.solo" :volume="mixer.volume" :current-bar="currentBar" :bar-count="barCount"
                   :disabled="followsAudio" @toggle-shown="toggleShown" @toggle-mute="toggleMute" @toggle-solo="toggleSolo"
                   @volume="setVolume" @go-to-bar="goToBar" />
  </section>
</template>

<style scoped>
.tab-viewer { display: grid; gap: .5rem; min-width: 0; }
.toolbar { display: flex; flex-wrap: wrap; gap: .5rem; align-items: center; }
.toolbar.view { gap: .75rem; }
.inline { display: inline-flex; gap: .35rem; align-items: center; width: auto; }
.inline select { width: auto; }
.play { min-width: 44px; min-height: 44px; }
.hint { margin: 0; }
.controls { display: grid; gap: .5rem; min-width: 0; }
.tempo { width: 8rem; padding: 0; }
.inline output { min-width: 3.2rem; font-variant-numeric: tabular-nums; }
/* #32 : un seul bloc à la hauteur du parent ; seule la partition défile, commandes et pistes restent visibles */
.tab-viewer.fill { display: flex; flex-direction: column; height: 100%; min-height: 0; }
.fill .controls { flex: none; }
.fill .desk { flex: 1; min-height: 0; overflow: auto; }
.fill .desk.pending { flex: none; }
.fill > :deep(.track-panel) { flex: none; }
.sync { display: flex; flex-wrap: wrap; gap: .5rem; align-items: center; }
.sync p { margin: 0; flex-basis: 100%; }
/* #14 : la partition suit la largeur, sans défilement horizontal à 360 px */
.score { width: 100%; max-width: 100%; overflow: hidden; background: #fff; color: #000; }
/* #31 : la page de Guitar Pro, sur son bureau gris ; à moins de 720 px, la page prend toute la largeur */
.desk { background: #d9d9d9; padding: 1.5rem; border-radius: 4px; }
.desk .score { max-width: 1100px; margin: 0 auto; box-shadow: 0 1px 4px rgb(0 0 0 / 30%); }
/* Caché tant que la partition n'est pas prête, mais à sa largeur finale : alphaTab ne redessine pas à l'affichage */
.desk.pending { height: 0; padding-block: 0; overflow: hidden; }
@media (max-width: 719px) {
  .desk { padding: 0; background: none; }
  .desk .score { box-shadow: none; border-radius: 4px; }
}
/* Curseur de lecture et sélection, dessinés par alphaTab hors du gabarit de Vue */
.score :deep(.at-cursor-bar) { background: rgb(255 242 0 / 25%); }
.score :deep(.at-selection div) { background: rgb(64 64 255 / 10%); }
.score :deep(.at-cursor-beat) { background: rgb(64 64 255 / 75%); width: 3px; }
.score :deep(.at-highlight *) { fill: #0078ff; stroke: #0078ff; }
</style>