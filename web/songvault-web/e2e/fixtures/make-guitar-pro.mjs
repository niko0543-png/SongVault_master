// #28 : fabrique les fichiers Guitar Pro des tests, demo.gp5 et demo.gp, à partir d'un riff écrit ici.
// #30 : et demo-b.gp, le même riff retouché, pour la comparaison (une mesure insérée au début, une note changée).
// Lancer depuis web/songvault-web : node e2e/fixtures/make-guitar-pro.mjs (les fichiers produits sont versionnés).
// demo.gp5 est écrit octet par octet au format Guitar Pro 5.00 (le minimum que lisent Guitar Pro et alphaTab) ;
// demo.gp (Guitar Pro 7 et 8) est exporté par alphaTab depuis le même riff.
import { writeFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import * as alphaTab from '@coderline/alphatab'

const TITLE = 'Demo SongVault'
// Une note par temps, en noires, 4 temps par mesure : [corde (1 = mi aigu), case]
const BARS = [
  [[6, 0], [6, 3], [5, 0], [5, 2]],
  [[4, 0], [4, 2], [3, 0], [3, 2]],
  [[2, 0], [2, 1], [1, 0], [1, 3]],
  [[1, 0], [2, 1], [3, 0], [4, 2]],
]
const TUNING = [64, 59, 55, 50, 45, 40]                 // E4 B3 G3 D3 A2 E2 (MIDI), de la corde 1 à la 6

// ---- Guitar Pro 5.00, petit-boutiste
const bytes = []
const byte = (b) => bytes.push(b & 0xff)
const int16 = (v) => { byte(v); byte(v >> 8) }
const int32 = (v) => { byte(v); byte(v >> 8); byte(v >> 16); byte(v >> 24) }
const latin1 = (s) => [...s].map((c) => c.charCodeAt(0))
const fixed = (s, size) => { const b = latin1(s); byte(b.length); for (let i = 0; i < size; i++) byte(b[i] ?? 0) }
const intByteString = (s) => { const b = latin1(s); int32(b.length + 1); byte(b.length); b.forEach(byte) }

fixed('FICHIER GUITAR PRO v5.00', 30)
for (const text of [TITLE, '', 'SongVault', '', '', '', '', '', '']) intByteString(text)   // titre… instructions
int32(0)                                                // lignes de notice
int32(0)                                                // paroles : aucune piste
for (let i = 0; i < 5; i++) { int32(0); int32(0) }
for (let i = 0; i < 28; i++) byte(0)                   // mise en page
int16(0)
for (let i = 0; i < 10; i++) intByteString('')         // en-têtes et pieds de page
intByteString('Moderato')                               // tempo : nom, valeur
int32(90)
int32(0)                                                // tonalité
byte(0)                                                 // octave
for (let i = 0; i < 64; i++) { int32(i === 0 ? 25 : 0); byte(13); byte(8); for (let j = 0; j < 6; j++) byte(0) }  // canaux MIDI : guitare acier
for (let i = 0; i < 19; i++) int16(-1)                 // coda, segno… : aucun
int32(0)
int32(BARS.length)
int32(1)                                                // une piste
BARS.forEach((_, i) => {
  if (i === 0) { byte(0x03); byte(4); byte(4); for (let j = 0; j < 4; j++) byte(2) }   // 4/4 et groupes de croches
  else byte(0)
  byte(0); byte(0); byte(0)                             // fins alternatives, swing, réservé
})
byte(0x08)                                              // piste visible
fixed('Guitare', 40)
int32(TUNING.length)
for (let i = 0; i < 7; i++) int32(TUNING[i] ?? 0)
int32(1); int32(1); int32(2); int32(24)                 // port, canal, canal d'effets, nombre de cases
int32(0)                                                // capodastre
byte(0xff); byte(0); byte(0); byte(0)                   // couleur
byte(0x03)                                              // tablature et portée
byte(0); byte(0); byte(0); byte(0)
int32(0); int32(0); int32(0)
for (let i = 0; i < 10; i++) byte(0)
byte(0); byte(0)
for (let i = 0; i < 16; i++) byte(0)                   // banque RSE
for (const notes of BARS) {
  byte(0)
  int32(notes.length)                                   // voix 1
  for (const [string, fret] of notes) {
    byte(0)                                             // battue simple
    byte(0)                                             // noire
    byte(1 << (7 - string))                             // corde 1 = bit 6
    byte(0x20); byte(1); byte(fret)                     // note normale, case
    byte(0)
    int16(0)
  }
  int32(0)                                              // voix 2 : vide
}
const gp5 = Uint8Array.from(bytes)

// ---- Guitar Pro 7 : le même riff en alphaTex, exporté par alphaTab
const settings = new alphaTab.Settings()
const toGp = (bars) => {
  const tex = `\\title "${TITLE}" \\tempo 90 . \\track "Guitare" ` + bars.map((bar) => bar.map(([s, f]) => `${f}.${s}.4`).join(' ')).join(' | ')
  return new alphaTab.exporter.Gp7Exporter().export(alphaTab.importer.ScoreLoader.loadAlphaTex(tex, settings), settings)
}
const gp = toGp(BARS)
// #30 : B = une mesure ajoutée au début, et la 3e note de la 2e mesure du riff passe de la case 0 à 2
const BARS_B = [[[1, 12], [1, 10], [1, 8], [1, 7]], BARS[0], BARS[1].map((n, i) => (i === 2 ? [3, 2] : n)), BARS[2], BARS[3]]
const gpB = toGp(BARS_B)

// ---- Vérification : alphaTab relit les trois fichiers
for (const [name, data, bars] of [['demo.gp5', gp5, BARS], ['demo.gp', gp, BARS], ['demo-b.gp', gpB, BARS_B]]) {
  const read = alphaTab.importer.ScoreLoader.loadScoreFromBytes(data, settings)
  const notes = read.tracks[0].staves[0].bars.flatMap((b) => b.voices[0].beats.flatMap((beat) => beat.notes)).length
  if (read.title !== TITLE || read.masterBars.length !== bars.length || notes !== bars.length * 4) throw new Error(`${name} illisible`)
  writeFileSync(fileURLToPath(new URL(name, import.meta.url)), data)
  console.log(`${name} : ${data.length} octets, ${read.masterBars.length} mesures, ${notes} notes`)
}