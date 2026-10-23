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

  // La couleur double la liste : des éléments verts (ajout) et orange (modification) dans la partition
  const score = page.getByTestId('tab-score')
  await expect(score.locator('[fill="#1A7F37"], [fill="#1a7f37"], [style*="26, 127, 55"]').first()).toBeAttached()
  await expect(score.locator('[fill="#B35900"], [fill="#b35900"], [style*="179, 89, 0"]').first()).toBeAttached()

  // #36 : la liste est à côté de la partition, qui défile seule dans son cadre
  const panel = await page.getByTestId('gp-changes-panel').boundingBox()
  const frame = await score.boundingBox()
  expect(panel!.x + panel!.width).toBeLessThanOrEqual(frame!.x)

  // Clic sur une différence : la mesure est sélectionnée dans la partition
  await changes.nth(1).click()
  await expect(score.locator('.at-selection div').first()).toBeVisible()
  expect(violations).toEqual([])
})