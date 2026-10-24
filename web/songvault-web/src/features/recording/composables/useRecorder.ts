import { onBeforeUnmount, ref, shallowRef } from 'vue'
import {
  CLIP_THRESHOLD, extensionFor, microphoneConstraints, peakOf, pickFormat, recordingFileName,
  RECORDING_BITS_PER_SECOND, RECORDING_TIMESLICE_MS,
} from '../recorder'
import { t } from '@/shared/i18n'

export type RecorderState = 'idle' | 'starting' | 'recording' | 'paused' | 'stopped'

export interface Recording {
  blob: Blob
  fileName: string
  durationMs: number
  startedAt: Date
}

/** Le navigateur sait enregistrer le micro (HTTPS ou localhost, MediaRecorder présent). */
export function isRecordingSupported(): boolean {
  return typeof window !== 'undefined' && typeof window.MediaRecorder !== 'undefined'
    && !!navigator.mediaDevices?.getUserMedia && pickFormat((type) => MediaRecorder.isTypeSupported(type)) !== null
}

function errorMessage(error: unknown): string {
  const name = error instanceof DOMException ? error.name : ''
  if (name === 'NotAllowedError' || name === 'SecurityError') return t('recording.errors.denied')
  if (name === 'NotFoundError' || name === 'OverconstrainedError') return t('recording.errors.noMicrophone')
  if (name === 'NotReadableError') return t('recording.errors.busy')
  return t('recording.errors.failed')
}

/**
 * Micro → MediaRecorder (ADR 0049). Le son reste en mémoire jusqu'à l'arrêt (environ 70 Mo par heure) ; l'écran est
 * gardé allumé (Wake Lock), car un téléphone en veille coupe le micro de la page.
 */
export function useRecorder() {
  const state = ref<RecorderState>('idle')
  const error = ref<string | null>(null)
  const elapsedMs = ref(0)
  const level = ref(0)                                          // crête des 100 dernières ms, de 0 à 1
  const clipped = ref(false)                                    // saturation dans les 2 dernières secondes
  const devices = ref<MediaDeviceInfo[]>([])
  const deviceId = ref<string>('')                              // '' : micro par défaut
  const recording = shallowRef<Recording | null>(null)

  let stream: MediaStream | null = null
  let recorder: MediaRecorder | null = null
  let audioContext: AudioContext | null = null
  let analyser: AnalyserNode | null = null
  let samples: Float32Array<ArrayBuffer> | null = null
  let chunks: Blob[] = []
  let ticker: ReturnType<typeof setInterval> | null = null
  let wakeLock: WakeLockSentinel | null = null
  let startedAt = new Date()
  let accumulatedMs = 0                                         // durée des passages déjà terminés (pauses)
  let segmentStart = 0
  let lastClipAt = -Infinity

  async function refreshDevices() {
    try {
      const all = await navigator.mediaDevices.enumerateDevices()
      devices.value = all.filter((device) => device.kind === 'audioinput' && device.deviceId)
    } catch {
      devices.value = []
    }
  }

  async function keepScreenOn() {
    try {
      wakeLock = (await navigator.wakeLock?.request('screen')) ?? null
    } catch {
      wakeLock = null                                           // refusé (batterie faible) : tant pis
    }
  }

  function onVisibilityChange() {
    // Le verrou tombe quand la page est masquée ; on le reprend au retour
    if (document.visibilityState === 'visible' && (state.value === 'recording' || state.value === 'paused')) void keepScreenOn()
  }

  function tick() {
    if (state.value === 'recording') elapsedMs.value = accumulatedMs + performance.now() - segmentStart
    if (!analyser || !samples) return
    analyser.getFloatTimeDomainData(samples)
    level.value = state.value === 'recording' ? peakOf(samples) : 0
    const now = performance.now()
    if (level.value >= CLIP_THRESHOLD) lastClipAt = now
    clipped.value = now - lastClipAt < 2000
  }

  function release() {
    if (ticker) clearInterval(ticker)
    ticker = null
    stream?.getTracks().forEach((track) => track.stop())
    stream = null
    void audioContext?.close().catch(() => undefined)
    audioContext = null
    analyser = null
    samples = null
    void wakeLock?.release().catch(() => undefined)
    wakeLock = null
    document.removeEventListener('visibilitychange', onVisibilityChange)
    level.value = 0
  }

  async function start() {
    if (state.value !== 'idle') return
    const format = pickFormat((type) => MediaRecorder.isTypeSupported(type))
    if (!format) {
      error.value = t('recording.errors.unsupported')
      return
    }
    error.value = null
    state.value = 'starting'
    try {
      stream = await navigator.mediaDevices.getUserMedia({ audio: microphoneConstraints(deviceId.value || null) })
    } catch (e) {
      release()
      state.value = 'idle'
      error.value = errorMessage(e)
      return
    }
    void refreshDevices()                                       // les noms des micros ne sont donnés qu'après l'accord

    try {
      audioContext = new AudioContext()
      analyser = audioContext.createAnalyser()
      analyser.fftSize = 2048
      samples = new Float32Array(analyser.fftSize)
      audioContext.createMediaStreamSource(stream).connect(analyser)
    } catch {
      analyser = null                                           // sans indicateur de niveau, on enregistre quand même
    }

    chunks = []
    recorder = new MediaRecorder(stream, { mimeType: format.mimeType, audioBitsPerSecond: RECORDING_BITS_PER_SECOND })
    recorder.ondataavailable = (event) => { if (event.data.size > 0) chunks.push(event.data) }
    recorder.onstop = () => {
      const mimeType = recorder?.mimeType || format.mimeType
      const durationMs = elapsedMs.value
      release()
      recording.value = {
        blob: new Blob(chunks, { type: mimeType.split(';')[0] }),
        fileName: recordingFileName(startedAt, extensionFor(mimeType, format.extension)),
        durationMs,
        startedAt,
      }
      chunks = []
      state.value = 'stopped'
    }
    recorder.onerror = () => {
      error.value = t('recording.errors.interrupted')
      if (recorder?.state !== 'inactive') recorder?.stop()
    }
    // Micro débranché ou coupé par le système : on garde ce qui a été pris
    stream.getAudioTracks()[0]?.addEventListener('ended', () => {
      if (recorder && recorder.state !== 'inactive') {
        error.value = t('recording.errors.interrupted')
        stop()
      }
    })

    startedAt = new Date()
    accumulatedMs = 0
    elapsedMs.value = 0
    segmentStart = performance.now()
    recorder.start(RECORDING_TIMESLICE_MS)
    state.value = 'recording'
    ticker = setInterval(tick, 100)
    document.addEventListener('visibilitychange', onVisibilityChange)
    void keepScreenOn()
  }

  function pause() {
    if (state.value !== 'recording' || !recorder) return
    recorder.pause()
    accumulatedMs += performance.now() - segmentStart
    elapsedMs.value = accumulatedMs
    state.value = 'paused'
  }

  function resume() {
    if (state.value !== 'paused' || !recorder) return
    recorder.resume()
    segmentStart = performance.now()
    state.value = 'recording'
  }

  function stop() {
    if (!recorder || recorder.state === 'inactive') return
    if (ticker) clearInterval(ticker)
    ticker = null
    if (state.value === 'recording') accumulatedMs += performance.now() - segmentStart
    elapsedMs.value = accumulatedMs
    recorder.stop()                                             // onstop rend le fichier
  }

  /** Oublie l'enregistrement terminé (envoyé, ou jeté par « Recommencer »). */
  function reset() {
    recording.value = null
    elapsedMs.value = 0
    error.value = null
    state.value = 'idle'
  }

  onBeforeUnmount(() => {
    if (recorder && recorder.state !== 'inactive') {
      recorder.onstop = null                                    // page quittée (confirmé) : rien à garder
      recorder.stop()
    }
    release()
  })

  return { state, error, elapsedMs, level, clipped, devices, deviceId, recording, refreshDevices, start, pause, resume, stop, reset }
}
