import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { expect, test } from '@playwright/test'
import { createServer, type Plugin, type ViteDevServer } from 'vite'
import vue from '@vitejs/plugin-vue'
import { alphaTab } from '@coderline/alphatab-vite'

/**
 * #30 : la comparaison de deux fichiers Guitar Pro dans Chromium, avec le vrai alphaTab et le vrai Web Worker,
 * sous la même CSP que nginx. Vite sert la comparaison seule (e2e/harness/guitar-pro-compare.html).
 */
const CSP = "default-src 'self'; connect-src 'self'; img-src 'self' data:; media-src 'self'; style-src 'self' 'unsafe-inline'; frame-ancestors 'none'"

const root = fileURLToPath(new URL('..', import.meta.url))
let server: ViteDevServer

/** L'adresse de lecture d'un fichier, comme l'API : …/files/{id}/content → e2e/fixtures/{id}.gp */
const fixtures: Plugin = {
  name: 'fixtures',
  configureServer(s) {
    s.middlewares.use((req, res, next) => {
      const match = /\/files\/([\w-]+)\/content$/.exec(req.url ?? '')
      if (!match) return next()
      res.setHeader('Content-Type', 'application/octet-stream')
      res.end(readFileSync(fileURLToPath(new URL(`fixtures/${match[1]}.gp`, import.meta.url))))
    })
  },
}

test.beforeAll(async () => {
  server = await createServer({
    configFile: false, root, logLevel: 'error',
    // Son propre cache : lancé en même temps que guitar-pro.spec.ts, il ne doit pas recharger la page de l'autre
    cacheDir: 'node_modules/.vite-e2e-compare',
    plugins: [vue(), alphaTab(), fixtures],
    worker: { format: 'es' },
    resolve: { alias: { '@': fileURLToPath(new URL('../src', import.meta.url)) } },
    server: { port: 0, host: '127.0.0.1', headers: { 'Content-Security-Policy': CSP } },
  })
  await server.listen()
})
test.afterAll(async () => server?.close())

test('liste les différences, les colore dans la partition, et un clic place la partition sur la mesure', async ({ page }) => {
  test.setTimeout(90_000)                                   // premier passage : Vite prépare alphaTab
  const violations: string[] = []
  page.on('console', (message) => {
    if (message.type() === 'error' && /Content Security Policy/i.test(message.text())) violations.push(message.text())
  })
  await page.goto(`${server.resolvedUrls!.local[0]}e2e/harness/guitar-pro-compare.html`)

  // Le calcul passe par le Web Worker : la liste arrive avec exactement les deux changements de demo-b.gp
  const changes = page.getByTestId('gp-changes').getByRole('button')
  await expect(changes).toHaveText(['Mesure 1 : ajoutée', 'Mesure 3 : 1 note modifiée, 1 note retirée'], { timeout: 30_000 })
  await expect(page.getByTestId('tab-play')).toBeEnabled({ timeout: 30_000 })

  // La couleur double la liste : dans la nouvelle partition, ajouts en vert et modifications en orange, rien en rouge
  const score = page.getByTestId('tab-score')
  const green = '[fill="#1A7F37"], [fill="#1a7f37"], [style*="26, 127, 55"]'
  const orange = '[fill="#B35900"], [fill="#b35900"], [style*="179, 89, 0"]'
  const red = '[fill="#B42318"], [fill="#b42318"], [style*="180, 35, 24"]'
  await expect(score.locator(green).first()).toBeAttached()
  await expect(score.locator(orange).first()).toBeAttached()
  await expect(score.locator(red)).toHaveCount(0)
  // #37 : le panneau des pistes reste en bas
  await expect(page.getByTestId('tab-tracks')).toBeVisible()

  // #36 : la liste est à côté de la partition, qui défile seule dans son cadre
  const panel = await page.getByTestId('gp-changes-panel').boundingBox()
  const frame = await score.boundingBox()
  expect(panel!.x + panel!.width).toBeLessThanOrEqual(frame!.x)

  // Clic sur une différence : la mesure est sélectionnée dans la partition
  await changes.nth(1).click()
  await expect(score.locator('.at-selection div').first()).toBeVisible()

  // #37 : l'ancienne partition, avec en orange la note remplacée (demo-b.gp n'enlève aucune mesure), et rien en vert
  // et l'on reste sur la même musique : la mesure 3 de B est la mesure 2 de A
  await page.getByTestId('gp-side-old').check()
  await expect(page.getByTestId('tab-play')).toBeEnabled({ timeout: 30_000 })
  await expect(page.getByTestId('tab-bar')).toHaveAttribute('aria-label', 'Mesure 2 sur 4')
  await expect(page.getByTestId('tab-score').locator(orange).first()).toBeAttached({ timeout: 30_000 })
  await expect(page.getByTestId('tab-score').locator(green)).toHaveCount(0)

  // Retour à la nouvelle partition, sur la même mesure
  await page.getByTestId('gp-side-new').check()
  await expect(page.getByTestId('tab-bar')).toHaveAttribute('aria-label', 'Mesure 3 sur 5', { timeout: 30_000 })
  expect(violations).toEqual([])
})

test('#37 : en changeant de version, on reste sur la mesure cliquée, ou sur celle du haut si on a seulement fait défiler', async ({ page }) => {
  test.setTimeout(90_000)
  await page.goto(`${server.resolvedUrls!.local[0]}e2e/harness/guitar-pro-compare.html?a=demo-tracks&b=demo-tracks`)
  await expect(page.getByTestId('tab-play')).toBeEnabled({ timeout: 30_000 })
  const desk = page.locator('.desk')
  const bar = page.getByTestId('tab-bar')
  const ready = () => expect(page.getByTestId('tab-play')).toBeEnabled({ timeout: 30_000 })

  // Un clic sur une note plus bas dans la partition : le curseur y va, puis on passe à l'ancienne version
  await desk.evaluate((frame) => { frame.scrollTop = 600 })
  const box = (await desk.boundingBox())!
  await page.mouse.click(box.x + box.width / 2, box.y + 150)
  await expect(bar).not.toHaveAttribute('aria-label', 'Mesure 1 sur 16')
  const clicked = await bar.getAttribute('aria-label')
  await page.getByTestId('gp-side-old').check()
  await ready()
  await expect(bar).toHaveAttribute('aria-label', clicked!)
  await expect.poll(() => desk.evaluate((frame) => frame.scrollTop)).toBeGreaterThan(300)   // la partition n'est pas revenue au début
  const cursor = await page.locator('.at-cursor-beat').boundingBox()
  expect(cursor!.y).toBeGreaterThan(box.y)
  expect(cursor!.y).toBeLessThan(box.y + box.height)                                       // le curseur est à l'écran

  // Sans clic : on fait défiler tout en bas, la nouvelle version s'ouvre sur la première mesure visible
  await desk.evaluate((frame) => { frame.scrollTop = frame.scrollHeight })
  await page.getByTestId('gp-side-new').check()
  await ready()
  await expect.poll(async () => Number((await bar.getAttribute('aria-label'))!.split(' ')[1])).toBeGreaterThan(8)
})
