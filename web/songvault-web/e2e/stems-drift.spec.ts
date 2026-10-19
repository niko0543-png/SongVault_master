import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import ts from 'typescript'
import { expect, test, type Page } from '@playwright/test'

/**
 * #23 : dérive des pistes du mélangeur, mesurée dans le vrai moteur Web Audio de Chromium.
 * Pas besoin de la pile Docker : le moteur (src/features/stems/mixerEngine.ts, qui n'importe rien) est chargé
 * dans une page vide, et un OfflineAudioContext calcule 3 minutes de son en une seconde.
 *
 * Deux pistes de tops (un échantillon à 1, le reste à 0) : la piste A, à gauche, à chaque seconde ;
 * la piste B, à droite, une demi-seconde plus tard. Calées, les tops de B suivent ceux de A d'exactement 24 000 échantillons.
 */
const RATE = 48_000
const SECONDS = 180

async function loadEngine(page: Page) {
  const source = readFileSync(resolve(process.cwd(), 'src/features/stems/mixerEngine.ts'), 'utf8')
  const js = ts.transpileModule(source, {
    compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ESNext },
  }).outputText
  await page.addScriptTag({ type: 'module', content: `${js}\nwindow.__stems = { createMixer };` })
  await page.waitForFunction(() => '__stems' in window)
}

interface Clicks { left: number[]; right: number[] }

/** Rend le mélange hors ligne ; seekAt : à cette seconde, on saute à seekTo (comme le curseur de position). */
function render(page: Page, options: { seekAt?: number; seekTo?: number } = {}) {
  return page.evaluate(async ({ rate, seconds, seekAt, seekTo }): Promise<Clicks> => {
    const { createMixer } = (window as unknown as { __stems: typeof import('../src/features/stems/mixerEngine') }).__stems
    const ctx = new OfflineAudioContext(2, rate * (seconds + 1), rate)

    function clickTrack(channel: number, firstClick: number) {
      const buffer = ctx.createBuffer(2, rate * seconds, rate)
      const data = buffer.getChannelData(channel)
      for (let i = firstClick; i < data.length; i += rate) data[i] = 1
      return buffer
    }

    const mixer = createMixer(ctx, [clickTrack(0, 0), clickTrack(1, rate / 2)])
    mixer.start(0)
    if (seekAt !== undefined && seekTo !== undefined) {
      void ctx.suspend(seekAt).then(() => {
        mixer.start(seekTo)
        void ctx.resume()
      })
    }
    const out = await ctx.startRendering()

    const peaks = (data: Float32Array) => {
      const found: number[] = []
      for (let i = 0; i < data.length; i++) if (data[i]! > 0.25) found.push(i)
      return found
    }
    return { left: peaks(out.getChannelData(0)), right: peaks(out.getChannelData(1)) }
  }, { rate: RATE, seconds: SECONDS, ...options })
}

/** Pour chaque top de A dans la fenêtre, l'écart avec le top de B qui suit, moins la demi-seconde attendue. */
function drifts(clicks: Clicks, fromSample: number, toSample: number) {
  return clicks.left
    .filter((i) => i >= fromSample && i < toSample)
    .map((i) => (clicks.right.find((j) => j > i) ?? Infinity) - i - RATE / 2)
}

test.beforeEach(async ({ page }) => loadEngine(page))

test('les pistes restent calées à l’échantillon près après 3 minutes de lecture', async ({ page }) => {
  const clicks = await render(page)

  expect(clicks.left).toHaveLength(SECONDS)
  expect(clicks.right).toHaveLength(SECONDS)
  // Toutes partent au même instant : 0,1 s après start(), soit 4 800 échantillons
  expect(Math.abs(clicks.left[0]! - 4_800)).toBeLessThanOrEqual(1)
  const late = drifts(clicks, (SECONDS - 10) * RATE, Infinity)       // les 10 dernières secondes
  expect(late.length).toBeGreaterThanOrEqual(9)
  for (const d of late) expect(Math.abs(d)).toBeLessThanOrEqual(1)
  // Et pas un top de A ne s'est décalé de sa place en 3 minutes
  clicks.left.forEach((i, k) => expect(Math.abs(i - (4_800 + k * RATE))).toBeLessThanOrEqual(1))
})

test('les pistes restent calées après un saut dans le morceau', async ({ page }) => {
  const clicks = await render(page, { seekAt: 60, seekTo: 150 })

  const afterSeek = drifts(clicks, 61 * RATE, Infinity)
  expect(afterSeek.length).toBeGreaterThanOrEqual(25)                 // de 150 s à la fin des pistes
  for (const d of afterSeek) expect(Math.abs(d)).toBeLessThanOrEqual(1)
})