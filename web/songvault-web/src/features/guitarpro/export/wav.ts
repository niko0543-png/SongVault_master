/**
 * #49 : fichiers WAV des pistes créées depuis le Guitar Pro (ADR 0049). PCM 16 bits stéréo, comme un export
 * de Guitar Pro : sans perte, lu partout, et le serveur en tire l'aperçu MP3 du mélangeur (ADR 0021).
 */
export const SAMPLE_RATE = 44100
export const CHANNELS = 2
const BYTES_PER_SAMPLE = 2
const HEADER_BYTES = 44

/** Taille du fichier pour une durée donnée : sert à annoncer ce que les pistes prendront dans le quota. */
export function wavBytes(durationMs: number, sampleRate = SAMPLE_RATE) {
  const frames = Math.ceil((durationMs / 1000) * sampleRate)
  return HEADER_BYTES + frames * CHANNELS * BYTES_PER_SAMPLE
}

/** Échantillons entrelacés de -1 à 1 → entiers 16 bits ; au-delà de ±1, le son est écrêté plutôt que retourné. */
export function toPcm16(samples: Float32Array): Int16Array {
  const out = new Int16Array(samples.length)
  for (let i = 0; i < samples.length; i++) {
    const s = Math.max(-1, Math.min(1, samples[i]!))
    out[i] = s < 0 ? Math.round(s * 0x8000) : Math.round(s * 0x7fff)
  }
  return out
}

/** En-tête RIFF/WAVE d'un fichier PCM dont les données font `dataBytes` octets. */
export function wavHeader(dataBytes: number, sampleRate = SAMPLE_RATE): ArrayBuffer {
  const buffer = new ArrayBuffer(HEADER_BYTES)
  const view = new DataView(buffer)
  const text = (offset: number, value: string) => {
    for (let i = 0; i < value.length; i++) view.setUint8(offset + i, value.charCodeAt(i))
  }
  text(0, 'RIFF')
  view.setUint32(4, 36 + dataBytes, true)
  text(8, 'WAVE')
  text(12, 'fmt ')
  view.setUint32(16, 16, true)                                   // taille du bloc « fmt »
  view.setUint16(20, 1, true)                                    // PCM
  view.setUint16(22, CHANNELS, true)
  view.setUint32(24, sampleRate, true)
  view.setUint32(28, sampleRate * CHANNELS * BYTES_PER_SAMPLE, true)
  view.setUint16(32, CHANNELS * BYTES_PER_SAMPLE, true)
  view.setUint16(34, BYTES_PER_SAMPLE * 8, true)
  text(36, 'data')
  view.setUint32(40, dataBytes, true)
  return buffer
}

/** Le fichier complet, à partir des morceaux convertis au fil du rendu (jamais tout le son en flottants d'un coup). */
export function wavBlob(chunks: readonly Int16Array[], sampleRate = SAMPLE_RATE): Blob {
  const dataBytes = chunks.reduce((sum, c) => sum + c.byteLength, 0)
  return new Blob([wavHeader(dataBytes, sampleRate), ...chunks.map((c) => c.buffer as ArrayBuffer)], { type: 'audio/wav' })
}
