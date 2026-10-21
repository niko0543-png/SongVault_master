import { fileURLToPath } from 'node:url'
import { expect, test } from '@playwright/test'
import { createServer, type ViteDevServer } from 'vite'
import vue from '@vitejs/plugin-vue'

/**
 * #25 : « affichage lisible sur mobile », mesuré dans Chromium à 360 px. Pas besoin de la pile Docker :
 * Vite sert la grille d'accords seule (e2e/harness/chords.html) avec la vraie feuille de style de l'application.
 */
test.use({ viewport: { width: 360, height: 780 }, hasTouch: true, isMobile: true })

const root = fileURLToPath(new URL('..', import.meta.url))
let server: ViteDevServer

test.beforeAll(async () => {
  server = await createServer({
    configFile: false, root, logLevel: 'error',
    plugins: [vue()],
    resolve: { alias: { '@': fileURLToPath(new URL('../src', import.meta.url)) } },
    server: { port: 0, host: '127.0.0.1' },
  })
  await server.listen()
})
test.afterAll(async () => server?.close())

interface Box { left: number; top: number; bottom: number }
interface Piece { chord: string; chordBox: Box; lyricBox: Box }

test('à 360 px, les accords restent au-dessus de leur syllabe et rien ne défile à l’horizontale', async ({ page }) => {
  await page.goto(`${server.resolvedUrls!.local[0]}e2e/harness/chords.html`)
  await page.waitForSelector('body[data-ready] .piece')

  const layout = await page.evaluate(() => {
    const box = (el: Element) => {
      const r = el.getBoundingClientRect()
      return { left: r.left, top: r.top, bottom: r.bottom }
    }
    const lines = [...document.querySelectorAll('.line.chorded')].map((line) => ({
      words: [...line.querySelectorAll('.word')].map((word) =>
        [...word.querySelectorAll('.piece')].map((piece) => ({
          chord: piece.querySelector('.chord')!.textContent ?? '',
          chordBox: box(piece.querySelector('.chord')!),
          lyricBox: box(piece.querySelector('.lyric')!),
        }))),
    }))
    return {
      lines,
      scroll: document.documentElement.scrollWidth,
      client: document.documentElement.clientWidth,
      fontSize: parseFloat(getComputedStyle(document.querySelector('.lyric')!).fontSize),
    }
  })

  expect(layout.scroll, 'la page défile à l’horizontale').toBeLessThanOrEqual(layout.client)
  expect(layout.fontSize).toBeGreaterThanOrEqual(16)
  expect(layout.lines).toHaveLength(3)

  const pieces: Piece[] = layout.lines.flatMap((l) => l.words.flat())
  for (const p of pieces.filter((p) => p.chord)) {
    expect(Math.abs(p.chordBox.left - p.lyricBox.left), `${p.chord} n’est pas calé sur sa syllabe`).toBeLessThan(1)
    expect(p.chordBox.bottom, `${p.chord} n’est pas au-dessus de sa syllabe`).toBeLessThanOrEqual(p.lyricBox.top + 1)
  }

  // Un mot ne se coupe jamais entre deux accords : « s’ar[Cadd9]rête » tient sur une seule ligne
  for (const word of layout.lines.flatMap((l) => l.words).filter((w) => w.length > 1)) {
    const tops = word.map((p) => Math.round(p.lyricBox.top))
    expect(new Set(tops).size, word.map((p) => p.chord).join(' ')).toBe(1)
  }

  // Les deux couplets sont trop longs pour 360 px : ils passent bien à la ligne
  for (const line of layout.lines.slice(0, 2)) {
    expect(new Set(line.words.map((w) => Math.round(w[0]!.lyricBox.top))).size).toBeGreaterThan(1)
  }
})