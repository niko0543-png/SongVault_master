import { describe, expect, it, vi } from 'vitest'
import type { AlphaTabApi } from '@coderline/alphatab'
import { connectExternalMedia } from '../externalMedia'

/** #29 : l'élément <audio> commandé par alphaTab, et qui lui donne l'heure. */
function setup() {
  const output: { handler?: Record<string, unknown> & { seekTo(ms: number): void; play(): void; pause(): void; playbackRate: number }; updatePosition: ReturnType<typeof vi.fn> } =
    { updatePosition: vi.fn() }
  const api = { player: { output }, pause: vi.fn() } as unknown as AlphaTabApi
  const audio = Object.assign(new EventTarget(), {
    currentTime: 0, duration: 12.5, paused: true, playbackRate: 1, volume: 1,
    play: vi.fn(async () => { audio.paused = false }),
    pause: vi.fn(() => { audio.paused = true }),
  })
  const disconnect = connectExternalMedia(api, audio as unknown as HTMLMediaElement)
  return { output, audio, disconnect }
}

describe('connectExternalMedia (#29)', () => {
  it('donne la durée à alphaTab et suit ses ordres : lecture, pause, vitesse', () => {
    const { output, audio } = setup()
    const handler = output.handler!

    expect(handler.backingTrackDuration).toBe(12500)
    handler.play()
    expect(audio.play).toHaveBeenCalled()
    handler.pause()
    expect(audio.pause).toHaveBeenCalled()
    handler.playbackRate = 0.75
    expect(audio.playbackRate).toBe(0.75)
  })

  it('n’applique que le dernier de plusieurs déplacements demandés d’affilée', async () => {
    const { output, audio } = setup()
    const set = vi.fn()
    let time = 0
    Object.defineProperty(audio, 'currentTime', { get: () => time, set: (v: number) => { set(v); time = v } })

    output.handler!.seekTo(2000)
    output.handler!.seekTo(0)
    output.handler!.seekTo(2000.26)
    await Promise.resolve()

    expect(set.mock.calls).toEqual([[2.00026]])
  })

  it('donne l’instant de la maquette après un déplacement, et débranche tout', () => {
    const { output, audio, disconnect } = setup()
    audio.currentTime = 4.5
    audio.dispatchEvent(new Event('seeked'))
    expect(output.updatePosition).toHaveBeenLastCalledWith(4500)

    disconnect()
    audio.dispatchEvent(new Event('seeked'))
    expect(output.updatePosition).toHaveBeenCalledTimes(1)
    expect(output.handler).toBeUndefined()
  })
})