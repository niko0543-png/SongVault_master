import { describe, expect, it, vi } from 'vitest'
import { synth } from '@coderline/alphatab'
import { CHANNELS, SAMPLE_RATE, toPcm16, wavBlob, wavBytes, wavHeader } from '../export/wav'
import { SOUND_FONT_GAIN } from '../soundFont'
import { exportOptions, renderStem, stemFileName, type ExportAudio } from '../export/renderStem'

/** #49 : WAV des pistes créées depuis le Guitar Pro, et rendu piste par piste (ADR 0049). */
describe('wav', () => {
  it('convertit en 16 bits en écrêtant au-delà de ±1', () => {
    expect([...toPcm16(new Float32Array([0, 1, -1, 0.5, 2, -3]))]).toEqual([0, 32767, -32768, 16384, 32767, -32768])
  })

  it('écrit un en-tête PCM stéréo 44,1 kHz 16 bits', () => {
    const view = new DataView(wavHeader(1000))
    const text = (o: number) => String.fromCharCode(...new Uint8Array(view.buffer, o, 4))
    expect([text(0), text(8), text(12), text(36)]).toEqual(['RIFF', 'WAVE', 'fmt ', 'data'])
    expect(view.getUint32(4, true)).toBe(1036)
    expect(view.getUint16(20, true)).toBe(1)
    expect(view.getUint16(22, true)).toBe(CHANNELS)
    expect(view.getUint32(24, true)).toBe(SAMPLE_RATE)
    expect(view.getUint32(28, true)).toBe(SAMPLE_RATE * 4)
    expect(view.getUint16(34, true)).toBe(16)
    expect(view.getUint32(40, true)).toBe(1000)
  })

  it('assemble les morceaux et annonce la taille d’une durée', async () => {
    const blob = wavBlob([new Int16Array([1, 2]), new Int16Array([3, 4, 5, 6])])
    expect(blob.size).toBe(44 + 12)
    expect(new Int16Array((await blob.arrayBuffer()).slice(44))).toEqual(new Int16Array([1, 2, 3, 4, 5, 6]))
    expect(wavBytes(1000)).toBe(44 + SAMPLE_RATE * 4)
  })
})

describe('renderStem', () => {
  const bank = new Uint8Array([1])

  it('ne laisse entendre que la piste choisie, ou le métronome seul pour le clic', () => {
    const track = exportOptions({ kind: 'track', index: 1 }, 3, bank)
    expect([...track.trackVolume]).toEqual([[0, 0], [1, 1], [2, 0]])
    expect(track.metronomeVolume).toBe(0)
    expect(track.soundFonts).toEqual([bank])
    expect(track.useSyncPoints).toBe(false)
    expect(track.masterVolume).toBe(SOUND_FONT_GAIN)
    const click = exportOptions({ kind: 'click' }, 2, bank)
    expect([...click.trackVolume]).toEqual([[0, 0], [1, 0]])
    expect(click.metronomeVolume).toBe(1)
  })

  function fakeExport(chunks: number[][]) {
    const destroy = vi.fn<() => void>()
    let i = 0
    const exportAudio = vi.fn<ExportAudio>(async () => ({
      render: async () => {
        const samples = chunks[i]
        if (!samples) return undefined
        i++
        return Object.assign(new synth.AudioExportChunk(), { samples: new Float32Array(samples), currentTime: i * 500, endTime: 1000 })
      },
      destroy,
      [Symbol.dispose]: destroy,
    }) as synth.IAudioExporter)
    return { exportAudio, destroy }
  }

  it('rend le morceau entier en WAV, avec la progression', async () => {
    const { exportAudio, destroy } = fakeExport([[0.5, -0.5], [1, 0]])
    const progress: number[] = []
    const stem = await renderStem(exportAudio, exportOptions({ kind: 'track', index: 0 }, 1, bank), { onProgress: (r) => progress.push(r) })

    expect(stem.wav.size).toBe(44 + 8)
    expect(stem.durationMs).toBe(1000)
    expect(stem.silent).toBe(false)
    expect(progress).toEqual([0.5, 1])
    expect(destroy).toHaveBeenCalledOnce()
  })

  it('signale une piste sans un son', async () => {
    const { exportAudio } = fakeExport([[0, 0, 0, 0]])
    expect((await renderStem(exportAudio, exportOptions({ kind: 'click' }, 1, bank))).silent).toBe(true)
  })

  it('s’arrête quand on annule, et libère le rendu', async () => {
    const { exportAudio, destroy } = fakeExport([[0.1, 0.1], [0.1, 0.1]])
    const abort = new AbortController()
    abort.abort()
    await expect(renderStem(exportAudio, exportOptions({ kind: 'click' }, 1, bank), { signal: abort.signal })).rejects.toThrow()
    expect(destroy).toHaveBeenCalledOnce()
  })

  it('nomme le fichier d’après la tablature et la piste', () => {
    expect(stemFileName('Nocturne.gp', 'Guitare 1')).toBe('Nocturne - Guitare 1.wav')
    expect(stemFileName('a.b.gp5', 'Voix/Chœurs: "lead"')).toBe('a.b - Voix_Chœurs_ _lead_.wav')
  })
})
