import { model } from '@coderline/alphatab'
import type { ScoreDiff } from './scoreDiff'

/**
 * #30 : colore dans la partition B les notes ajoutées (vert) et modifiées (orange), et la barre des mesures
 * ajoutées. La couleur double seulement la liste des différences, qui reste la référence (accessibilité).
 * Les couleurs gardent un contraste d'au moins 4,5:1 sur le fond blanc de la partition.
 */
export const ADDED_COLOR = '#1a7f37'
export const MODIFIED_COLOR = '#b35900'

const NOTE_PARTS = [
  model.NoteSubElement.StandardNotationNoteHead,
  model.NoteSubElement.StandardNotationAccidentals,
  model.NoteSubElement.GuitarTabFretNumber,
  model.NoteSubElement.NumberedNumber,
]
const BAR_PARTS = [
  model.BarSubElement.StandardNotationBarNumber,
  model.BarSubElement.GuitarTabsBarNumber,
  model.BarSubElement.StandardNotationBarLines,
  model.BarSubElement.GuitarTabsBarLines,
]

function color(hex: string): model.Color {
  const value = Number.parseInt(hex.slice(1), 16)
  return new model.Color((value >> 16) & 255, (value >> 8) & 255, value & 255)
}

function paintNote(note: model.Note, hex: string) {
  note.style = note.style ?? new model.NoteStyle()
  for (const part of NOTE_PARTS) note.style.colors.set(part, color(hex))
}

function paintBar(bar: model.Bar, hex: string) {
  bar.style = bar.style ?? new model.BarStyle()
  for (const part of BAR_PARTS) bar.style.colors.set(part, color(hex))
}

export function highlight(score: model.Score, diff: ScoreDiff) {
  for (const track of diff.tracks) {
    const staves = score.tracks[track.trackB]?.staves ?? []
    for (const change of track.changes) {
      if (change.kind === 'added') {
        for (const staff of staves) {
          const bar = staff.bars[change.barB]
          if (!bar) continue
          paintBar(bar, ADDED_COLOR)
          for (const voice of bar.voices) for (const beat of voice.beats) for (const note of beat.notes) paintNote(note, ADDED_COLOR)
        }
      } else if (change.kind === 'modified') {
        const bar = staves[0]?.bars[change.barB]
        if (bar) paintBar(bar, MODIFIED_COLOR)
        for (const ref of change.notes) {
          const note = staves[0]?.bars[ref.bar]?.voices[0]?.beats[ref.beat]?.notes[ref.note]
          if (note) paintNote(note, MODIFIED_COLOR)
        }
      }
    }
  }
}