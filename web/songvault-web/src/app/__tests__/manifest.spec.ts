// @vitest-environment node
// Lit des fichiers du disque : environnement Node, pas jsdom (où import.meta.url n'est pas un chemin de fichier)
import { existsSync, readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

const publicDir = fileURLToPath(new URL('../../../public/', import.meta.url))
const manifest = JSON.parse(readFileSync(`${publicDir}manifest.webmanifest`, 'utf8')) as {
  name: string; short_name: string; start_url: string; display: string; theme_color: string
  icons: { src: string; sizes: string; purpose: string }[]
}

describe('manifest.webmanifest', () => {
  it("contient ce qu'Android exige pour proposer l'installation", () => {
    expect(manifest.name).toBe('SongVault')
    expect(manifest.short_name.length).toBeLessThanOrEqual(12)   // sous l'icône de l'écran d'accueil
    expect(manifest.start_url).toBe('/')
    expect(manifest.display).toBe('standalone')
    expect(manifest.icons.map((i) => `${i.sizes} ${i.purpose}`)).toEqual(
      expect.arrayContaining(['192x192 any', '512x512 any', '512x512 maskable']))
  })

  it('chaque icône déclarée existe dans public/', () => {
    for (const icon of manifest.icons) expect(existsSync(`${publicDir}${icon.src.slice(1)}`), icon.src).toBe(true)
    expect(existsSync(`${publicDir}icons/apple-touch-icon.png`)).toBe(true)   // iOS, déclarée dans index.html
  })

  it("la couleur de thème est celle de l'application", () => {
    const css = readFileSync(fileURLToPath(new URL('../../assets/main.css', import.meta.url)), 'utf8')
    expect(css).toContain(`--primary: ${manifest.theme_color};`)
  })
})