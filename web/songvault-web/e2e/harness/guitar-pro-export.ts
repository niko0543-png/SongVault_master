import '@/assets/main.css'
import { createApp, h, ref } from 'vue'
import { createPinia } from 'pinia'
import GuitarProViewer from '@/features/guitarpro/components/GuitarProViewer.vue'
import { exportOptions, renderStem, type StemSource } from '@/features/guitarpro/export/renderStem'
import { SOUND_FONT_URL } from '@/features/guitarpro/soundFont'

/**
 * #49 : le lecteur sur ?file=…, et window.renderStem(source) qui rend une piste comme TrackAudioExport, puis la
 * relit avec le décodeur du navigateur : le test vérifie un vrai WAV, avec du son, de la durée du morceau.
 */
const file = new URLSearchParams(location.search).get('file') ?? 'demo-tracks.gp'
const viewer = ref<InstanceType<typeof GuitarProViewer> | null>(null)

declare global {
  interface Window {
    viewerReady: () => boolean
    renderStem: (source: StemSource) => Promise<{ bytes: number; durationMs: number; silent: boolean; decodedMs: number; rms: number[] }>
  }
}

window.viewerReady = () => viewer.value?.status === 'ready'
window.renderStem = async (source) => {
  const bank = new Uint8Array(await (await fetch(SOUND_FONT_URL)).arrayBuffer())
  const stem = await renderStem((o) => viewer.value!.exportAudio(o), exportOptions(source, viewer.value!.tracks.length, bank))
  const audio = await new OfflineAudioContext(2, 1, 44100).decodeAudioData(await stem.wav.arrayBuffer())
  // Énergie par seconde du canal gauche : où la piste joue
  const left = audio.getChannelData(0)
  const rms: number[] = []
  for (let start = 0; start < left.length; start += audio.sampleRate) {
    let sum = 0
    const end = Math.min(left.length, start + audio.sampleRate)
    for (let i = start; i < end; i++) sum += left[i]! * left[i]!
    rms.push(Math.sqrt(sum / (end - start)))
  }
  return { bytes: stem.wav.size, durationMs: stem.durationMs, silent: stem.silent, decodedMs: audio.duration * 1000, rms }
}

createApp({ render: () => h(GuitarProViewer, { ref: viewer, src: `/e2e/fixtures/${file}`, label: file }) })
  .use(createPinia())
  .mount('#viewer')
