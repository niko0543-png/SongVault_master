import { describe, expect, it } from 'vitest'
import { GAIN_SMOOTHING_SECONDS, START_DELAY_SECONDS, createMixer } from '../mixerEngine'
import { FakeAudioContext, asContext, fakeBuffer } from './fakeAudio'

describe('createMixer', () => {
  it('démarre toutes les pistes au même instant avec le même décalage', () => {
    const ctx = new FakeAudioContext()
    ctx.currentTime = 12.5
    const mixer = createMixer(asContext(ctx), [fakeBuffer(180), fakeBuffer(180), fakeBuffer(175)])

    const when = mixer.start(30)

    expect(when).toBe(12.5 + START_DELAY_SECONDS)
    expect(ctx.sources).toHaveLength(3)
    for (const source of ctx.sources) expect(source.start).toHaveBeenCalledWith(when, 30)
    expect(mixer.isPlaying).toBe(true)
  })

  it('ne lance pas une piste plus courte que la position demandée', () => {
    const ctx = new FakeAudioContext()
    const mixer = createMixer(asContext(ctx), [fakeBuffer(180), fakeBuffer(60)])

    mixer.start(90)

    expect(ctx.sources).toHaveLength(1)
    expect(mixer.duration).toBe(180)
  })

  it('suit la position sur l’horloge audio et la garde à l’arrêt', () => {
    const ctx = new FakeAudioContext()
    const mixer = createMixer(asContext(ctx), [fakeBuffer(180)])
    mixer.start(10)

    ctx.currentTime = START_DELAY_SECONDS + 5
    expect(mixer.position()).toBeCloseTo(15)
    mixer.stop()
    ctx.currentTime += 60

    expect(mixer.isPlaying).toBe(false)
    expect(mixer.position()).toBeCloseTo(15)
    expect(ctx.sources[0]!.stop).toHaveBeenCalled()
  })

  it('recrée toutes les sources pour aller à une autre position', () => {
    const ctx = new FakeAudioContext()
    const mixer = createMixer(asContext(ctx), [fakeBuffer(180), fakeBuffer(180)])
    mixer.start(0)

    mixer.start(42)

    expect(ctx.sources).toHaveLength(4)
    expect(ctx.sources[0]!.stop).toHaveBeenCalled()
    expect(ctx.sources[1]!.stop).toHaveBeenCalled()
    expect(ctx.sources[2]!.start).toHaveBeenCalledWith(START_DELAY_SECONDS, 42)
    expect(ctx.sources[3]!.start).toHaveBeenCalledWith(START_DELAY_SECONDS, 42)
  })

  it('règle le gain de chaque piste en douceur', () => {
    const ctx = new FakeAudioContext()
    const mixer = createMixer(asContext(ctx), [fakeBuffer(10), fakeBuffer(10)])
    const [, voice, drums] = ctx.gains                // le premier gain est le gain général

    mixer.setGains([0, 0.5])

    expect(voice!.gain.setTargetAtTime).toHaveBeenCalledWith(0, 0, GAIN_SMOOTHING_SECONDS)
    expect(drums!.gain.setTargetAtTime).toHaveBeenCalledWith(0.5, 0, GAIN_SMOOTHING_SECONDS)
  })

  it('repart du début une fois toutes les pistes finies', () => {
    const ctx = new FakeAudioContext()
    const mixer = createMixer(asContext(ctx), [fakeBuffer(10), fakeBuffer(8)])
    mixer.start(5)

    ctx.sources[1]!.onended!()
    expect(mixer.isPlaying).toBe(true)
    ctx.sources[0]!.onended!()

    expect(mixer.isPlaying).toBe(false)
    expect(mixer.position()).toBe(0)
  })
})