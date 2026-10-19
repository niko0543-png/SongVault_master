import { vi } from 'vitest'

/** Faux AudioContext : assez pour vérifier ce que le moteur programme, sans le moindre son (jsdom n'a pas Web Audio). */
export class FakeAudioContext {
  currentTime = 0
  readonly destination = {}
  readonly gains: { gain: { value: number; setTargetAtTime: ReturnType<typeof vi.fn>; cancelScheduledValues: ReturnType<typeof vi.fn> } }[] = []
  readonly sources: { buffer: unknown; start: ReturnType<typeof vi.fn>; stop: ReturnType<typeof vi.fn>; onended: (() => void) | null }[] = []
  readonly close = vi.fn(async () => {})

  createGain() {
    const node = {
      gain: {
        value: 1,
        setTargetAtTime: vi.fn((v: number) => { node.gain.value = v }),
        cancelScheduledValues: vi.fn(),
      },
      connect: vi.fn(), disconnect: vi.fn(),
    }
    this.gains.push(node)
    return node
  }

  createBufferSource() {
    const source = { buffer: null as unknown, start: vi.fn(), stop: vi.fn(), connect: vi.fn(), disconnect: vi.fn(), onended: null }
    this.sources.push(source)
    return source
  }

  async decodeAudioData(data: ArrayBuffer) {
    return fakeBuffer(new Uint8Array(data)[0] ?? 0)
  }
}

/** Un AudioBuffer réduit à sa durée. */
export function fakeBuffer(duration: number) {
  return { duration } as AudioBuffer
}

export function asContext(ctx: FakeAudioContext) {
  return ctx as unknown as BaseAudioContext
}