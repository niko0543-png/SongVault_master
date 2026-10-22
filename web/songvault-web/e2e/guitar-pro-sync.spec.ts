import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import type { IncomingMessage, ServerResponse } from 'node:http'
import { expect, test, type Page } from '@playwright/test'
import { createServer, type Plugin, type ViteDevServer } from 'vite'
import vue from '@vitejs/plugin-vue'
import { alphaTab } from '@coderline/alphatab-vite'

/**
 * #29 : la tablature suit une vraie maquette dans Chromium (élément <audio> et alphaTab en mode « média externe »),
 * sous la même CSP que nginx. Vite sert le lecteur seul (e2e/harness/guitar-pro-sync.html) ; ce fichier joue l'API.
 * demo.gp5 : 4 mesures à 90 BPM (2,67 s chacune avec le son de la tablature). La maquette les joue en 1 s chacune,
 * à partir de 2 s : sans les points de synchronisation, le curseur serait à la traîne.
 */
const CSP = "default-src 'self'; connect-src 'self'; img-src 'self' data:; media-src 'self'; style-src 'self' 'unsafe-inline'; frame-ancestors 'none'"
const SAVED = [2000, 3000, 4000, 5000].map((ms, i) => ({ barIndex: i, barOccurence: 0, barPosition: 0, millisecondOffset: ms }))

const root = fileURLToPath(new URL('..', import.meta.url))
let server: ViteDevServer

/** 10 s de silence, en WAV 8 kHz mono 16 bits : assez pour la lecture, sans fichier binaire dans le dépôt. */
function silence(seconds: number) {
  const data = Buffer.alloc(8000 * 2 * seconds)
  const header = Buffer.alloc(44)
  header.write('RIFF', 0); header.writeUInt32LE(36 + data.length, 4); header.write('WAVEfmt ', 8)
  header.writeUInt32LE(16, 16); header.writeUInt16LE(1, 20); header.writeUInt16LE(1, 22)
  header.writeUInt32LE(8000, 24); header.writeUInt32LE(16000, 28); header.writeUInt16LE(2, 32); header.writeUInt16LE(16, 34)
  header.write('data', 36); header.writeUInt32LE(data.length, 40)
  return Buffer.concat([header, data])
}
const wav = silence(10)

/** Sert le fichier par morceaux (Range), comme le stockage : sans cela, Chromium ne sait pas se déplacer dans l'audio. */
function sendRange(req: IncomingMessage, res: ServerResponse, body: Buffer, type: string) {
  const match = /bytes=(\d*)-(\d*)/.exec(req.headers.range ?? '')
  res.setHeader('Accept-Ranges', 'bytes')
  res.setHeader('Content-Type', type)
  if (!match) return res.end(body)
  const start = Number(match[1] || 0)
  const end = match[2] ? Math.min(Number(match[2]), body.length - 1) : body.length - 1
  res.statusCode = 206
  res.setHeader('Content-Range', `bytes ${start}-${end}/${body.length}`)
  res.end(body.subarray(start, end + 1))
}

const syncs = new Map<string, unknown>()

const api: Plugin = {
  name: 'api',
  configureServer(s) {
    s.middlewares.use((req, res, next) => {
      const url = req.url ?? ''
      if (/\/files\/\w+\/content$/.test(url)) {
        res.setHeader('Content-Type', 'application/octet-stream')
        return res.end(readFileSync(fileURLToPath(new URL('fixtures/demo.gp5', import.meta.url))))
      }
      if (url.endsWith('/files/demo/listen')) return sendRange(req, res, wav, 'audio/wav')
      const sync = /\/files\/(\w+)\/sync$/.exec(url)
      if (sync && req.method === 'PUT') {
        let body = ''
        req.on('data', (chunk) => { body += chunk })
        req.on('end', () => {
          const saved = { ...JSON.parse(body), audioFileName: 'maquette.wav' }
          syncs.set(sync[1]!, saved)
          res.setHeader('Content-Type', 'application/json')
          res.end(JSON.stringify(saved))
        })
        return
      }
      if (sync) {
        const saved = syncs.get(sync[1]!)
        if (!saved) { res.statusCode = 204; return res.end() }
        res.setHeader('Content-Type', 'application/json')
        return res.end(JSON.stringify(saved))
      }
      next()
    })
  },
}

test.beforeAll(async () => {
  syncs.set('saved', { audioFileId: 'demo', audioFileName: 'maquette.wav', points: SAVED })
  server = await createServer({
    configFile: false, root, logLevel: 'error',
    cacheDir: 'node_modules/.vite-e2e-sync',                // son propre cache, comme guitar-pro-compare.spec.ts
    plugins: [vue(), alphaTab(), api],
    worker: { format: 'es' },
    resolve: { alias: { '@': fileURLToPath(new URL('../src', import.meta.url)) } },
    server: { port: 0, host: '127.0.0.1', headers: { 'Content-Security-Policy': CSP } },
  })
  await server.listen()
})
test.afterAll(async () => server?.close())

async function open(page: Page, file: 'saved' | 'fresh') {
  const violations: string[] = []
  page.on('console', (message) => {
    if (message.type() === 'error' && /Content Security Policy/i.test(message.text())) violations.push(message.text())
  })
  await page.goto(`${server.resolvedUrls!.local[0]}e2e/harness/guitar-pro-sync.html?file=${file}`)
  return violations
}

/** L'instant de la maquette (s) et la mesure affichée, lus ensemble. */
const position = (page: Page) => page.evaluate(() => ({
  time: (document.querySelector('[data-testid="tab-audio"]') as HTMLAudioElement).currentTime,
  bar: document.querySelector('[data-testid="tab-bar"]')?.textContent?.trim(),
}))

test('une tablature calée suit sa maquette, y compris après un retour au début', async ({ page }) => {
  test.setTimeout(90_000)                                   // premier passage : Vite prépare alphaTab
  const violations = await open(page, 'saved')

  await expect(page.getByTestId('tab-source')).toHaveValue('audio', { timeout: 30_000 })
  const play = page.getByTestId('tab-play')
  await expect(play).toBeEnabled({ timeout: 30_000 })
  await play.click()

  // Pause après 4,2 s de maquette : on est dans la 3e mesure (calée de 4 à 5 s) ; le son de la tablature n'y serait qu'à 5,3 s
  await expect.poll(async () => (await position(page)).time, { timeout: 15_000 }).toBeGreaterThan(4.2)
  await play.click()
  await expect(play).toHaveAccessibleName('Lire la tablature')
  await expect.poll(async () => (await position(page)).bar).toBe('Mesure 3 sur 4')
  expect((await position(page)).time).toBeLessThan(5)

  // « Revenir au début » ramène la maquette au début de la 1re mesure, à 2 s, et non à 0
  await page.getByRole('button', { name: 'Revenir au début' }).click()
  await expect.poll(async () => (await position(page)).time, { timeout: 5_000 }).toBeCloseTo(2, 1)
  expect(violations).toEqual([])
})

test('on cale une tablature au tapé, puis elle suit la maquette', async ({ page }) => {
  test.setTimeout(90_000)
  const violations = await open(page, 'fresh')
  await expect(page.getByTestId('sync-start')).toBeEnabled({ timeout: 30_000 })
  await expect(page.getByTestId('tab-source')).toHaveCount(0)

  await page.getByTestId('sync-start').click()
  await expect(page.getByTestId('sync-tap')).toHaveText('Début de la mesure 1 sur 4')
  await page.getByTestId('sync-play').click()

  const audio = page.getByTestId('sync-audio')
  for (const second of [1, 2, 3, 4]) {
    await expect.poll(() => audio.evaluate((a: HTMLAudioElement) => a.currentTime), { timeout: 10_000 }).toBeGreaterThan(second)
    await page.getByTestId('sync-tap').click()
  }
  await expect(page.getByTestId('sync-count')).toHaveText('4 mesures calées sur 4.')
  await page.getByTestId('sync-save').click()

  await expect(page.getByTestId('tab-source')).toHaveValue('audio')
  await expect(page.getByTestId('sync-recorder')).toHaveCount(0)
  const saved = syncs.get('fresh') as { audioFileId: string; points: { barIndex: number; millisecondOffset: number }[] }
  expect(saved.audioFileId).toBe('demo')
  expect(saved.points.map((p) => p.barIndex)).toEqual([0, 1, 2, 3])
  saved.points.forEach((p, i) => expect(p.millisecondOffset).toBeGreaterThanOrEqual((i + 1) * 1000))
  expect(violations).toEqual([])
})