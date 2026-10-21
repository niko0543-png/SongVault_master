<script setup lang="ts">
import { onBeforeUnmount, reactive, ref, watch } from 'vue'
import { effectiveGains, newControl } from '../mix'
import type { Mixer } from '../mixerEngine'
import { usePlayerStore } from '@/features/player/stores/playerStore'
import { formatTime } from '@/shared/utils/format'
import { t } from '@/shared/i18n'

/** Le mélangeur (#23) : lecture des pistes ensemble, et pour chacune muet, solo et volume. */
const props = defineProps<{ engine: Mixer; names: readonly string[] }>()
const player = usePlayerStore()

const controls = reactive(props.names.map(newControl))
const isPlaying = ref(false)
const position = ref(0)

watch(controls, () => props.engine.setGains(effectiveGains(controls)), { deep: true, immediate: true })

let timer: ReturnType<typeof setInterval> | undefined
function refresh() {
  isPlaying.value = props.engine.isPlaying
  position.value = props.engine.position()
  if (!isPlaying.value) clearInterval(timer)
}

function play() {
  player.pause()                                    // un seul son à la fois : le lecteur du bas se met en pause
  props.engine.start(position.value >= props.engine.duration ? 0 : position.value)
  clearInterval(timer)
  timer = setInterval(refresh, 200)
  refresh()
}

function pause() {
  props.engine.stop()
  refresh()
}

function seek(seconds: number) {
  position.value = seconds
  if (props.engine.isPlaying) props.engine.start(seconds)
}

// Le lecteur du bas repart (autre fichier écouté) : le mélangeur se tait
watch(() => player.isPlaying, (playing) => { if (playing && props.engine.isPlaying) pause() })

onBeforeUnmount(() => {
  clearInterval(timer)
  props.engine.stop()
})
</script>

<template>
  <div class="mixer" role="region" :aria-label="t('stems.mixer.label')">
    <div class="transport">
      <button type="button" class="play" :aria-label="isPlaying ? t('stems.mixer.pause') : t('stems.mixer.play')"
              @click="isPlaying ? pause() : play()">
        {{ isPlaying ? '❚❚' : '▶' }}
      </button>
      <input class="seek" type="range" min="0" step="0.1" :max="engine.duration" :value="position"
             :aria-label="t('stems.mixer.position')" :aria-valuetext="t('shared.time.positionOf', { position: formatTime(position), duration: formatTime(engine.duration) })"
             @change="seek(Number(($event.target as HTMLInputElement).value))" />
      <span class="time">{{ formatTime(position) }} / {{ formatTime(engine.duration) }}</span>
    </div>

    <ul class="tracks">
      <li v-for="(name, i) in names" :key="i" data-testid="mixer-track" :class="{ silent: effectiveGains(controls)[i] === 0 }">
        <span class="name">{{ name }}</span>
        <button type="button" class="secondary toggle" :aria-pressed="controls[i]!.muted" :aria-label="t('stems.mixer.mute', { name })"
                :title="t('stems.mixer.muteShort')" @click="controls[i]!.muted = !controls[i]!.muted">M</button>
        <button type="button" class="secondary toggle" :aria-pressed="controls[i]!.solo" :aria-label="t('stems.mixer.solo', { name })"
                :title="t('stems.mixer.soloShort')" @click="controls[i]!.solo = !controls[i]!.solo">S</button>
        <input v-model.number="controls[i]!.volume" class="volume" type="range" min="0" max="1" step="0.05"
               :aria-label="t('stems.mixer.volume', { name })" />
      </li>
    </ul>
  </div>
</template>

<style scoped>
.transport { display: flex; gap: .75rem; align-items: center; margin-bottom: .75rem; }
.seek { flex: 1; min-width: 0; }
.time { font-variant-numeric: tabular-nums; }
.tracks { list-style: none; padding: 0; margin: 0; display: grid; gap: .4rem; }
.tracks li {
  display: grid; grid-template-columns: minmax(0, 1fr) auto auto minmax(6rem, 12rem); gap: .5rem; align-items: center;
  padding: .4rem .6rem; border: 1px solid var(--border); border-radius: var(--radius);
}
.tracks li.silent .name { opacity: .5; }
.name { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.toggle { padding: .2rem .6rem; font-weight: 600; }
.toggle[aria-pressed='true'] { background: var(--primary); border-color: var(--primary); color: var(--surface); }
@media (max-width: 600px) {
  .tracks li { grid-template-columns: minmax(0, 1fr) auto auto; }
  .volume { grid-column: 1 / -1; }
}
</style>