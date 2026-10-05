import { describe, expect, it } from 'vitest'
import { validateFile } from '../validation'
import type { FilePolicy } from '../types'

const policy: FilePolicy = { allowedExtensions: ['.mp3', '.pdf'], maxSizeBytes: 100 * 1024 * 1024 }
const file = (name: string, size = 1024) => ({ name, size })

describe('validateFile', () => {
  it('accepte une extension autorisée, quelle que soit la casse', () => {
    expect(validateFile(file('maquette.MP3'), policy)).toBeNull()
  })

  it.each(['virus.exe', 'chanson.mp3.exe', 'sans_extension'])('refuse %s', (name) => {
    expect(validateFile(file(name), policy)).toContain('non autorisé')
  })

  it('refuse un fichier trop volumineux', () => {
    expect(validateFile(file('long.mp3', 120 * 1024 * 1024), policy)).toContain('trop volumineux')
  })

  it('refuse un fichier vide', () => {
    expect(validateFile(file('vide.mp3', 0), policy)).toBe('Le fichier est vide.')
  })
})
