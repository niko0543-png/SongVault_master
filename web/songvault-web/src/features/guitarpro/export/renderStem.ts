import { synth } from '@coderline/alphatab'
import { SAMPLE_RATE, toPcm16, wavBlob } from './wav'
import { SOUND_FONT_GAIN } from '../soundFont'

/**
 * #49 : une piste audio rendue depuis le Guitar Pro (ADR 0049). alphaTab synthétise le morceau entier hors du temps
 * réel, dans son Web Worker, avec toutes les pistes à zéro sauf une : chaque fichier a la durée exacte du morceau,
 * et les pistes se calent d'elles-mêmes dans le mélangeur (ADR 0030).
 */
export type ExportAudio = (options: synth.AudioExportOptions) => Promise<synth.IAudioExporter>

/** Une piste de la partition (son index), ou le clic du métronome seul. */
export type StemSource = { kind: 'track'; index: number } | { kind: 'click' }

/** Durée demandée à chaque appel : assez pour peu d'allers-retours avec le worker, assez peu pour la progression. */
const CHUNK_MS = 5000

export function exportOptions(source: StemSource, trackCount: number, soundFont: Uint8Array): synth.AudioExportOptions {
  const options = new synth.AudioExportOptions()
  // Toujours la banque de sons passée ici : le synthétiseur du lecteur n'existe pas quand il suit une maquette (ADR 0036)
  options.soundFonts = [soundFont]
  options.sampleRate = SAMPLE_RATE
  options.useSyncPoints = false                                  // le tempo du fichier, pas celui d'une maquette
  options.masterVolume = SOUND_FONT_GAIN                        // même niveau que le lecteur, quelle que soit la banque
  options.metronomeVolume = source.kind === 'click' ? 1 : 0
  for (let i = 0; i < trackCount; i++) options.trackVolume.set(i, source.kind === 'track' && source.index === i ? 1 : 0)
  return options
}

export interface RenderedStem {
  wav: Blob
  durationMs: number
  /** Pas un échantillon au-dessus du silence : piste vide, ou instrument absent de la banque de sons. */
  silent: boolean
}

/** Rend une piste en WAV ; `signal` arrête entre deux morceaux de rendu. */
export async function renderStem(
  exportAudio: ExportAudio,
  options: synth.AudioExportOptions,
  { onProgress, signal }: { onProgress?: (ratio: number) => void; signal?: AbortSignal } = {},
): Promise<RenderedStem> {
  const exporter = await exportAudio(options)
  const chunks: Int16Array[] = []
  let durationMs = 0
  let peak = 0
  try {
    for (;;) {
      signal?.throwIfAborted()
      const chunk = await exporter.render(CHUNK_MS)
      if (!chunk) break
      const pcm = toPcm16(chunk.samples)
      for (const s of pcm) if (s > peak || -s > peak) peak = Math.abs(s)
      chunks.push(pcm)
      durationMs = chunk.endTime
      onProgress?.(chunk.endTime > 0 ? Math.min(1, chunk.currentTime / chunk.endTime) : 0)
    }
  } finally {
    exporter.destroy()
  }
  return { wav: wavBlob(chunks, options.sampleRate), durationMs, silent: peak === 0 }
}

/** « Nocturne.gp » et « Guitare 1 » → « Nocturne - Guitare 1.wav », sans les caractères refusés par Windows. */
export function stemFileName(tablatureFileName: string, stemName: string) {
  const dot = tablatureFileName.lastIndexOf('.')
  const base = dot > 0 ? tablatureFileName.slice(0, dot) : tablatureFileName
  const clean = (s: string) => s.replace(/[\\/:*?"<>|\u0000-\u001f]/g, '_').trim()
  return `${clean(base).slice(0, 120)} - ${clean(stemName).slice(0, 120)}.wav`
}
