import type { model } from '@coderline/alphatab'

/**
 * #30 : ce que la comparaison retient d'une partition alphaTab, en données simples.
 * Ces objets passent au Web Worker (structured clone) : ni classe ni référence vers alphaTab.
 */
export interface PlainNote {
  /** Corde au sens d'alphaTab (1 = la plus grave) et case : ce que l'on joue, pas la hauteur. */
  string: number
  fret: number
  /** Effets qui changent le jeu : liaison, glissé, bend, vibrato, harmonique… */
  effects: string
}

export interface PlainBeat {
  /** Durée (4 = noire), points, n-olet et silence, en une chaîne. */
  rhythm: string
  notes: PlainNote[]
  /** Empreinte du temps : rythme et notes ; deux temps identiques ont la même. */
  key: string
}

export interface PlainBar {
  /** Index de la mesure dans la partition (0 = première). */
  index: number
  beats: PlainBeat[]
  /** Empreinte de la mesure : chiffrage et temps de toutes les voix et portées. */
  key: string
}

export interface PlainTrack {
  index: number
  name: string
  /** Programme MIDI (25 = guitare acoustique, 33 = basse…). */
  program: number
  /** Accordage en hauteurs MIDI, de la corde la plus aiguë à la plus grave (comme alphaTab). */
  tuning: number[]
  capo: number
  bars: PlainBar[]
}

export interface PlainScore {
  tempo: number
  tracks: PlainTrack[]
}

function noteEffects(note: model.Note): string {
  const flags: string[] = []
  if (note.isTieDestination) flags.push('tie')
  if (note.isHammerPullOrigin) flags.push('hp')
  if (note.slideOutType) flags.push(`slide${note.slideOutType}`)
  if (note.hasBend) flags.push(`bend${note.bendType}`)
  if (note.vibrato) flags.push(`vib${note.vibrato}`)
  if (note.harmonicType) flags.push(`harm${note.harmonicType}`)
  if (note.isPalmMute) flags.push('pm')
  if (note.isLetRing) flags.push('ring')
  if (note.isDead) flags.push('dead')
  if (note.isGhost) flags.push('ghost')
  if (note.isStaccato) flags.push('stac')
  if (note.accentuated) flags.push(`acc${note.accentuated}`)
  return flags.join(',')
}

function plainBeat(beat: model.Beat): PlainBeat {
  const rhythm = [beat.duration, beat.dots, beat.tupletNumerator, beat.tupletDenominator, beat.isRest ? 'r' : ''].join('.')
  // Les notes gardent l'ordre d'alphaTab (les différences les désignent par leur rang) ; l'empreinte les trie par corde
  const notes = beat.notes.map((n) => ({ string: n.string, fret: n.fret, effects: noteEffects(n) }))
  const sorted = [...notes].sort((x, y) => x.string - y.string)
  const key = `${rhythm}[${sorted.map((n) => `${n.string}:${n.fret}${n.effects ? `(${n.effects})` : ''}`).join(' ')}]`
  return { rhythm, notes, key }
}

/** Les temps de la première voix de la première portée : ceux que la liste des différences détaille. */
function plainBar(track: model.Track, index: number): PlainBar {
  const master = track.score.masterBars[index]!
  const parts = [`${master.timeSignatureNumerator}/${master.timeSignatureDenominator}`]
  let beats: PlainBeat[] = []
  track.staves.forEach((staff, s) => {
    staff.bars[index]?.voices.forEach((voice, v) => {
      const voiceBeats = voice.beats.map(plainBeat)
      const main = s === 0 && v === 0
      if (main) beats = voiceBeats
      // Une voix secondaire vide ne compte pas : alphaTab en crée sans qu'elle soit écrite
      if (main || voiceBeats.some((b) => b.notes.length > 0)) parts.push(`${s}.${v}:${voiceBeats.map((b) => b.key).join(' ')}`)
    })
  })
  return { index, beats, key: parts.join('|') }
}

/** Partition alphaTab → données simples à comparer. */
export function toPlainScore(score: model.Score): PlainScore {
  return {
    tempo: score.tempo,
    tracks: score.tracks.map((track) => ({
      index: track.index,
      name: track.name,
      program: track.playbackInfo.program,
      tuning: [...(track.staves[0]?.tuning ?? [])],
      capo: track.staves[0]?.capo ?? 0,
      bars: score.masterBars.map((_, i) => plainBar(track, i)),
    })),
  }
}