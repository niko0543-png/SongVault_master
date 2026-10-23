<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { model } from '@coderline/alphatab'
import { PANEL_HEIGHT } from '../prefs'
import { t } from '@/shared/i18n'

/**
 * #31 : le panneau des pistes de Guitar Pro, collé en bas de l'écran pendant que la partition défile (ADR 0037).
 * Par piste : affichée ou non, muet, solo, volume ; à droite, la vue d'ensemble des mesures (case pleine = notes
 * jouées, à la couleur de la piste dans le fichier). Un clic sur une case place la partition sur la mesure.
 * Le composant ne parle pas à alphaTab : il émet, et GuitarProViewer agit.
 * #35 : la poignée du haut change la hauteur des pistes (glisser, ou ↑ ↓ au clavier), gardée par GuitarProViewer.
 */
const props = defineProps<{
  tracks: model.Track[]
  shown: number[]
  mute: boolean[]
  solo: boolean[]
  volume: number[]
  currentBar: number
  barCount: number
  /** #34 : « 0:42 / 2:31 », vide avant la lecture. */
  time?: string
  /** La tablature suit une maquette (#29) : muet, solo et volume n'agissent pas sur elle. */
  disabled: boolean
}>()
const emit = defineEmits<{
  toggleShown: [index: number]
  toggleMute: [index: number]
  toggleSolo: [index: number]
  volume: [index: number, value: number]
  goToBar: [index: number]
}>()

/** Largeur d'une case de la vue d'ensemble, en px (la même que dans le CSS). */
const CELL = 16

/** #33 : replié ou non ; GuitarProViewer le garde sur l'appareil. */
const open = defineModel<boolean>('open', { default: true })
/** #35 : hauteur des pistes en px ; null = celle par défaut (40 % de l'écran au plus). */
const height = defineModel<number | null>('height', { default: null })
const overview = ref<HTMLElement | null>(null)
const anySolo = computed(() => props.solo.some(Boolean))
/** #34 : une seule piste : ni case (elle reste affichée) ni muet/solo, mais le volume et la vue d'ensemble. */
const single = computed(() => props.tracks.length === 1)
const bars = computed(() => Array.from({ length: props.barCount }, (_, i) => i))
/** Pour chaque piste, les mesures où elle joue au moins une note. */
const played = computed(() => props.tracks.map((track) =>
  bars.value.map((b) => track.staves.some((staff) => staff.bars[b] !== undefined && !staff.bars[b]!.isRestOnly))))
const colorOf = (track: model.Track) => track.color?.rgba ?? '#8a8a8a'
/** Comme dans Guitar Pro : 1, puis toutes les 4 mesures. */
const numbered = (b: number) => b === 0 || (b + 1) % 4 === 0
const silent = (i: number) => props.mute[i] || (anySolo.value && !props.solo[i])

// #35 : redimensionner, comme le séparateur de Guitar Pro entre la partition et les pistes
const body = ref<HTMLElement | null>(null)
const KEY_STEP = 24
const maxHeight = () => Math.min(PANEL_HEIGHT.max, Math.round(window.innerHeight * 0.75))
const clampHeight = (value: number) => Math.round(Math.max(PANEL_HEIGHT.min, Math.min(maxHeight(), value)))
const bodyStyle = computed(() => (height.value === null ? undefined : { height: `${height.value}px`, maxHeight: 'none' }))
let drag: { y: number; height: number } | null = null

function startResize(event: PointerEvent) {
  if (!body.value) return
  drag = { y: event.clientY, height: body.value.getBoundingClientRect().height }
  ;(event.currentTarget as HTMLElement).setPointerCapture?.(event.pointerId)
  event.preventDefault()                                      // pas de sélection de texte pendant le glissé
}

function resize(event: PointerEvent) {
  if (drag) height.value = clampHeight(drag.height + drag.y - event.clientY)   // vers le haut = plus grand
}

function endResize() {
  drag = null
}

function resizeByKey(event: KeyboardEvent) {
  const current = height.value ?? body.value?.getBoundingClientRect().height ?? PANEL_HEIGHT.min
  const next = { ArrowUp: current + KEY_STEP, ArrowDown: current - KEY_STEP, Home: maxHeight(), End: PANEL_HEIGHT.min }[event.key]
  if (next === undefined) return
  event.preventDefault()
  height.value = clampHeight(next)
}

// La mesure jouée reste visible dans la vue d'ensemble
watch(() => props.currentBar, (bar) => {
  const box = overview.value
  if (!box) return
  const x = (bar - 1) * CELL
  if (x < box.scrollLeft || x + CELL > box.scrollLeft + box.clientWidth) box.scrollLeft = Math.max(0, x - 4 * CELL)
})
</script>

<template>
  <section class="track-panel" :aria-label="t('guitarpro.viewer.tracks')" data-testid="tab-tracks">
    <!-- #35 : poignée de redimensionnement (séparateur réglable : glisser, ou flèches haut et bas) -->
    <div v-if="open" class="grip" role="separator" tabindex="0" aria-orientation="horizontal" data-testid="tab-tracks-resize"
         :aria-label="t('guitarpro.viewer.resizeTracks')" :aria-valuemin="PANEL_HEIGHT.min" :aria-valuemax="PANEL_HEIGHT.max"
         :aria-valuenow="height ?? undefined" @pointerdown="startResize" @pointermove="resize" @pointerup="endResize"
         @pointercancel="endResize" @keydown="resizeByKey" @dblclick="height = null" />
    <div class="head">
      <button type="button" class="toggle" :aria-expanded="open" data-testid="tab-tracks-toggle" @click="open = !open">
        {{ t('guitarpro.viewer.tracks') }} <span aria-hidden="true">{{ open ? '▾' : '▴' }}</span>
      </button>
      <output>{{ t('guitarpro.viewer.bar', { bar: currentBar, total: barCount }) }}<template v-if="time"> · {{ time }}</template></output>
    </div>

    <div v-if="open" ref="body" class="body" :style="bodyStyle" data-testid="tab-tracks-body">
      <ul class="list">
        <li v-for="(track, i) in tracks" :key="i" class="row" :class="{ silent: silent(i) }">
          <span class="swatch" :style="{ background: colorOf(track) }" aria-hidden="true" />
          <label class="name">
            <input v-if="!single" type="checkbox" :checked="shown.includes(i)" :aria-label="t('guitarpro.viewer.show', { name: track.name })"
                   @change="emit('toggleShown', i)">
            <span>{{ track.name }}</span>
          </label>
          <template v-if="!single">
            <button type="button" class="chip mute" :aria-pressed="mute[i]" :disabled="disabled"
                    :aria-label="t('guitarpro.viewer.muteTrack', { name: track.name })" @click="emit('toggleMute', i)">M</button>
            <button type="button" class="chip solo" :aria-pressed="solo[i]" :disabled="disabled"
                    :aria-label="t('guitarpro.viewer.soloTrack', { name: track.name })" @click="emit('toggleSolo', i)">S</button>
          </template>
          <input class="volume" type="range" min="0" max="2" step="0.1" :value="volume[i]" :disabled="disabled"
                 :aria-label="t('guitarpro.viewer.volumeTrack', { name: track.name })"
                 @input="emit('volume', i, Number(($event.target as HTMLInputElement).value))">
        </li>
      </ul>

      <!-- Vue d'ensemble : la souris y trouve vite une mesure ; au clavier, la partition reste le chemin -->
      <div ref="overview" class="overview" aria-hidden="true" data-testid="tab-overview">
        <div class="grid" :style="{ width: `${barCount * CELL}px` }">
          <div class="numbers">
            <span v-for="b in bars" :key="b" class="number">{{ numbered(b) ? b + 1 : '' }}</span>
          </div>
          <div v-for="(track, i) in tracks" :key="i" class="lane" :class="{ silent: silent(i) }">
            <span v-for="b in bars" :key="b" class="cell" :style="played[i]![b] ? { background: colorOf(track) } : undefined"
                  @click="emit('goToBar', b)" />
          </div>
          <span class="now" :style="{ left: `${(currentBar - 1) * CELL}px` }" />
        </div>
      </div>
    </div>
  </section>
</template>

<style scoped>
/* Collé en bas de l'écran (au-dessus du lecteur audio s'il est ouvert) tant que la tablature est à l'écran */
.track-panel { position: sticky; bottom: var(--audio-player-height, 0px); z-index: 5;
               background: #2b2b30; color: #e8e8ec; border-radius: 4px; box-shadow: 0 -2px 8px rgb(0 0 0 / 25%); }
.grip { height: 8px; margin-bottom: -4px; cursor: ns-resize; touch-action: none; border-radius: 4px 4px 0 0; position: relative; }
.grip::after { content: ''; position: absolute; left: 50%; top: 3px; width: 36px; height: 3px; margin-left: -18px;
               border-radius: 2px; background: #6a6a74; }
.grip:hover::after, .grip:focus-visible::after { background: #c8c8d0; }
.head { display: flex; align-items: center; justify-content: space-between; gap: .5rem; padding: .25rem .5rem; }
.toggle { background: none; border: 0; color: inherit; font-weight: 600; padding: .25rem .5rem; }
.head output { font-size: .85rem; color: #b8b8c0; font-variant-numeric: tabular-nums; }
.body { --row: 2.25rem; display: grid; grid-template-columns: minmax(18rem, 24rem) 1fr; max-height: 40vh; overflow-y: auto;
        border-top: 1px solid #44444c; }
.list { list-style: none; margin: 0; padding: 1.25rem 0 0; }
.row { display: flex; align-items: center; gap: .4rem; height: var(--row); padding: 0 .5rem; border-bottom: 1px solid #3a3a42; }
.row.silent .name, .lane.silent { opacity: .45; }
.swatch { width: 4px; align-self: stretch; border-radius: 2px; }
.name { display: inline-flex; align-items: center; gap: .35rem; flex: 1; min-width: 0; width: auto; margin: 0; }
.name input { width: auto; flex: none; margin: 0; }
.name span { overflow: hidden; white-space: nowrap; text-overflow: ellipsis; }
.chip { min-width: 1.9rem; min-height: 0; height: 1.6rem; padding: 0 .4rem; background: #3d3d45; color: #e8e8ec;
        border: 1px solid #55555e; font-weight: 700; }
.mute[aria-pressed='true'] { background: #e0b400; border-color: #e0b400; color: #1d1d27; }
.solo[aria-pressed='true'] { background: #d33; border-color: #d33; color: #fff; }
.volume { width: 5rem; padding: 0; }
.overview { overflow-x: auto; }
.grid { position: relative; }
.numbers { display: flex; height: 1.25rem; font-size: .7rem; color: #b8b8c0; }
.number { flex: 0 0 16px; }
.lane { display: flex; height: var(--row); border-bottom: 1px solid #3a3a42; }
.cell { flex: 0 0 16px; height: 100%; border-right: 1px solid #3a3a42; cursor: pointer; }
.cell:hover { outline: 1px solid #fff; outline-offset: -1px; }
.now { position: absolute; top: 0; bottom: 0; width: 16px; border: 2px solid #fff; pointer-events: none; }
/* #14 : sur téléphone, les pistes seules, en cibles de 44 px ; la vue d'ensemble et le volume s'effacent */
@media (max-width: 719px) {
  .body { --row: 44px; grid-template-columns: 1fr; max-height: 45vh; }
  .list { padding-top: 0; }
  .overview, .volume { display: none; }
  .chip { height: 36px; min-width: 40px; }
}
</style>
