import { describe, expect, it } from 'vitest'
import {
  extensionFor, formatElapsed, microphoneConstraints, peakOf, pickFormat, recordingFileName, rehearsalTitle,
} from '../recorder'

describe('recorder', () => {
  it('choisit Opus dans WebM quand le navigateur le sait, sinon MP4 (Safari)', () => {
    expect(pickFormat(() => true)?.extension).toBe('.webm')
    expect(pickFormat((type) => type === 'audio/mp4')).toEqual({ mimeType: 'audio/mp4', extension: '.m4a' })
    expect(pickFormat(() => false)).toBeNull()
  })

  it("prend l'extension du type réellement produit", () => {
    expect(extensionFor('audio/webm;codecs=opus', '.m4a')).toBe('.webm')
    expect(extensionFor('audio/mp4;codecs=mp4a.40.2', '.webm')).toBe('.m4a')
    expect(extensionFor('audio/ogg', '.webm')).toBe('.ogg')
    expect(extensionFor('', '.m4a')).toBe('.m4a')
  })

  it('coupe les traitements faits pour la voix', () => {
    expect(microphoneConstraints(null)).toEqual({
      echoCancellation: false, noiseSuppression: false, autoGainControl: false, channelCount: { ideal: 2 },
    })
    expect(microphoneConstraints('usb-1').deviceId).toEqual({ exact: 'usb-1' })
  })

  it('nomme le fichier et le titre par la date', () => {
    const at = new Date(2026, 9, 24, 20, 5)
    expect(recordingFileName(at, '.webm')).toBe('repetition-2026-10-24-2005.webm')
    expect(rehearsalTitle(at)).toBe('Répétition du 24/10/2026')
  })

  it('affiche les heures au-delà de 60 minutes', () => {
    expect(formatElapsed(75_000)).toBe('1:15')
    expect(formatElapsed(3_723_000)).toBe('1:02:03')
    expect(formatElapsed(-5)).toBe('0:00')
  })

  it('mesure la crête', () => {
    expect(peakOf([0.1, -0.6, 0.3])).toBeCloseTo(0.6)
    expect(peakOf([1.4])).toBe(1)
    expect(peakOf([])).toBe(0)
  })
})
