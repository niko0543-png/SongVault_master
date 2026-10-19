import { describe, expect, it } from 'vitest'
import { effectiveGains, newControl, type TrackControl } from '../mix'

const track = (over: Partial<TrackControl> = {}): TrackControl => ({ ...newControl(), ...over })

describe('effectiveGains', () => {
  it('rend le volume de chaque piste quand rien n’est muet ni en solo', () => {
    expect(effectiveGains([track(), track({ volume: 0.5 })])).toEqual([1, 0.5])
  })

  it('coupe une piste muette', () => {
    expect(effectiveGains([track({ muted: true }), track()])).toEqual([0, 1])
  })

  it('ne laisse entendre que les pistes en solo', () => {
    expect(effectiveGains([track({ solo: true, volume: 0.8 }), track(), track({ solo: true })])).toEqual([0.8, 0, 1])
  })

  it('coupe une piste muette même en solo', () => {
    expect(effectiveGains([track({ solo: true, muted: true }), track()])).toEqual([0, 0])
  })

  it('revient aux volumes quand le dernier solo est retiré', () => {
    const controls = [track({ solo: true }), track({ volume: 0.3 })]
    controls[0]!.solo = false
    expect(effectiveGains(controls)).toEqual([1, 0.3])
  })

  it('borne le volume entre 0 et 1', () => {
    expect(effectiveGains([track({ volume: 1.4 }), track({ volume: -0.2 })])).toEqual([1, 0])
  })
})