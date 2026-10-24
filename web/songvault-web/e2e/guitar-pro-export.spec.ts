import { fileURLToPath } from 'node:url'
import { expect, test } from '@playwright/test'
import { createServer, type ViteDevServer } from 'vite'
import vue from '@vitejs/plugin-vue'
import { alphaTab } from '@coderline/alphatab-vite'

/**
 * #49 : les pistes audio créées depuis le Guitar Pro (ADR 0049), rendues par le vrai synthétiseur d'alphaTab dans
 * Chromium, sous la CSP de nginx. demo-tracks.gp : 16 mesures à 90 à la noire (42,7 s), une guitare tout du long
 * et une basse qui n'entre qu'à la mesure 5 (10,7 s) : chaque fichier ne doit faire entendre que sa piste.
 */
const CSP = "default-src 'self'; connect-src 'self'; img-src 'self' data:; media-src 'self'; style-src 'self' 'unsafe-inline'; frame-ancestors 'none'"
const SONG_MS = (16 * 4 * 60_000) / 90

/** Ce que le banc d'essai (harness/guitar-pro-export.ts) pose sur window. */
type Source = { kind: 'track'; index: number } | { kind: 'click' }
interface Rendered { bytes: number; durationMs: number; silent: boolean; decodedMs: number; rms: number[] }
type Harness = Window & { viewerReady: () => boolean; renderStem: (source: Source) => Promise<Rendered> }

const root = fileURLToPath(new URL('..', import.meta.url))
let server: ViteDevServer

test.beforeAll(async () => {
  server = await createServer({
    configFile: false, root, logLevel: 'error',
    cacheDir: 'node_modules/.vite-e2e-export',              // son propre cache, comme guitar-pro-sync.spec.ts
    plugins: [vue(), alphaTab()],
    worker: { format: 'es' },
    resolve: { alias: { '@': fileURLToPath(new URL('../src', import.meta.url)) } },
    server: { port: 0, host: '127.0.0.1', headers: { 'Content-Security-Policy': CSP } },
  })
  await server.listen()
})
test.afterAll(async () => server?.close())

test('chaque piste devient un WAV de la durée du morceau, avec sa piste seule', async ({ page }) => {
  test.setTimeout(120_000)
  const violations: string[] = []
  page.on('console', (m) => { if (m.type() === 'error' && /Content Security Policy/i.test(m.text())) violations.push(m.text()) })
  await page.goto(`${server.resolvedUrls!.local[0]}e2e/harness/guitar-pro-export.html?file=demo-tracks.gp`)
  await expect.poll(() => page.evaluate(() => (window as unknown as Harness).viewerReady()), { timeout: 30_000 }).toBe(true)

  const guitar = await page.evaluate(() => (window as unknown as Harness).renderStem({ kind: 'track', index: 0 }))
  const bass = await page.evaluate(() => (window as unknown as Harness).renderStem({ kind: 'track', index: 1 }))
  const click = await page.evaluate(() => (window as unknown as Harness).renderStem({ kind: 'click' }))

  for (const stem of [guitar, bass, click]) {
    expect(stem.silent).toBe(false)
    expect(Math.abs(stem.durationMs - SONG_MS)).toBeLessThan(1500)              // le morceau, et sa fin qui résonne
    expect(Math.abs(stem.decodedMs - stem.durationMs)).toBeLessThan(50)           // un WAV que le navigateur relit
    expect(stem.bytes).toBeGreaterThan(44 + 44100 * 4 * 40)                       // 16 bits stéréo à 44,1 kHz
  }
  // La guitare joue dès la première seconde ; la basse se tait sur les 4 premières mesures, puis joue
  const loud = (rms: number[], from: number, to: number) => Math.min(...rms.slice(from, to))
  const quiet = (rms: number[], from: number, to: number) => Math.max(...rms.slice(from, to))
  expect(loud(guitar.rms, 0, 40)).toBeGreaterThan(0.001)
  expect(quiet(bass.rms, 0, 10)).toBeLessThan(0.0001)
  expect(loud(bass.rms, 12, 40)).toBeGreaterThan(0.001)
  expect(violations).toEqual([])
})
