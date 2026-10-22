import { fileURLToPath } from 'node:url'
import { expect, test, type Page } from '@playwright/test'
import { createServer, type ViteDevServer } from 'vite'
import vue from '@vitejs/plugin-vue'
import { alphaTab } from '@coderline/alphatab-vite'

/**
 * #28 : la tablature s'affiche et se joue dans Chromium, sous la même politique de sécurité (CSP) que nginx.
 * Pas besoin de la pile Docker : Vite sert le lecteur seul (e2e/harness/guitar-pro.html) et les fichiers d'essai.
 */
const CSP = "default-src 'self'; connect-src 'self'; img-src 'self' data:; media-src 'self'; style-src 'self' 'unsafe-inline'; frame-ancestors 'none'"

const root = fileURLToPath(new URL('..', import.meta.url))
let server: ViteDevServer

test.beforeAll(async () => {
  server = await createServer({
    configFile: false, root, logLevel: 'error',
    plugins: [vue(), alphaTab()],
    worker: { format: 'es' },
    resolve: { alias: { '@': fileURLToPath(new URL('../src', import.meta.url)) } },
    server: { port: 0, host: '127.0.0.1', headers: { 'Content-Security-Policy': CSP } },
  })
  await server.listen()
})
test.afterAll(async () => server?.close())

/** Ouvre le banc d'essai et note toute règle CSP enfreinte. */
async function open(page: Page, file: string, query = '') {
  const violations: string[] = []
  page.on('console', (message) => {
    if (message.type() === 'error' && /Content Security Policy/i.test(message.text())) violations.push(message.text())
  })
  await page.goto(`${server.resolvedUrls!.local[0]}e2e/harness/guitar-pro.html?file=${file}${query}`)
  return violations
}

for (const file of ['demo.gp5', 'demo.gp']) {
  test(`${file} : la tablature s’affiche et le bouton lecture répond, sans entorse à la CSP`, async ({ page }) => {
    const violations = await open(page, file)

    const play = page.getByTestId('tab-play')
    await expect(play).toBeEnabled({ timeout: 30_000 })                 // partition dessinée et sons chargés
    await expect(page.getByTestId('tab-score').locator('svg').first()).toBeVisible()

    // Le curseur de lecture avance : le son est vraiment parti
    const cursor = page.locator('.at-cursor-beat')
    const start = await cursor.boundingBox()
    await play.click()
    await expect(play).toHaveAccessibleName('Mettre en pause')
    await expect.poll(async () => (await cursor.boundingBox())?.x, { timeout: 10_000 }).not.toBe(start?.x)
    await play.click()
    await expect(play).toHaveAccessibleName('Lire la tablature')
    expect(violations).toEqual([])
  })
}

test('à 360 px, la partition suit la largeur de l’écran', async ({ browser }) => {
  const page = await browser.newPage({ viewport: { width: 360, height: 780 }, isMobile: true, hasTouch: true })
  await open(page, 'demo.gp5')
  await expect(page.getByTestId('tab-play')).toBeEnabled({ timeout: 30_000 })
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
  expect(overflow).toBeLessThanOrEqual(0)
  await page.close()
})


test('#31 : la page blanche façon Guitar Pro, avec seulement la tablature et un zoom à 150 %', async ({ page }) => {
  const violations = await open(page, 'demo.gp5')
  await expect(page.getByTestId('tab-play')).toBeEnabled({ timeout: 30_000 })
  const score = page.getByTestId('tab-score')
  const full = (await score.boundingBox())!.height

  await page.getByTestId('tab-staves').selectOption('tab')                  // la portée disparaît
  await expect.poll(async () => (await score.boundingBox())!.height).toBeLessThan(full)
  const tabOnly = (await score.boundingBox())!.height
  await page.getByTestId('tab-zoom').selectOption('1.5')                    // tout grossit
  await expect.poll(async () => (await score.boundingBox())!.height).toBeGreaterThan(tabOnly)
  expect(violations).toEqual([])
})


test('#31 : le panneau des pistes reste en bas de l’écran pendant que la partition défile, et coupe une piste en pleine lecture', async ({ browser }) => {
  const page = await browser.newPage({ viewport: { width: 1280, height: 700 } })
  const violations = await open(page, 'demo-tracks.gp')
  const play = page.getByTestId('tab-play')
  await expect(play).toBeEnabled({ timeout: 30_000 })
  const panel = page.getByTestId('tab-tracks')

  await page.mouse.wheel(0, 400)                                            // la partition défile, le panneau reste collé en bas
  await expect.poll(async () => Math.round((await panel.boundingBox())!.y + (await panel.boundingBox())!.height)).toBe(700)
  await expect(panel).toBeVisible()

  // La page défile jusqu'au milieu de la partition (sans molette : la même distance quel que soit le système)
  await page.getByTestId('tab-score').evaluate((score) => {
    const box = score.getBoundingClientRect()
    window.scrollTo(0, window.scrollY + box.top + box.height / 2 - window.innerHeight / 2)
  })
  // La partition continue sous le bord de l'écran, et le panneau reste collé en bas (à un pixel près : arrondis)
  await expect.poll(async () => (await page.getByTestId('tab-score').boundingBox())!.y + (await page.getByTestId('tab-score').boundingBox())!.height)
    .toBeGreaterThan(700)
  await expect.poll(async () => {
    const box = (await panel.boundingBox())!
    return Math.abs(box.y + box.height - 700)
  }).toBeLessThanOrEqual(1)
  await play.click()
  await page.getByRole('button', { name: 'Couper Basse' }).click()
  await expect(page.getByRole('button', { name: 'Couper Basse' })).toHaveAttribute('aria-pressed', 'true')
  await expect(play).toHaveAccessibleName('Mettre en pause')               // la lecture continue
  await play.click()
  expect(violations).toEqual([])
  await page.close()
})

test('#32 : sur la page du lecteur, seule la partition défile ; commandes et pistes restent à l’écran, et le clavier pilote', async ({ browser }) => {
  const page = await browser.newPage({ viewport: { width: 1280, height: 700 } })
  const violations = await open(page, 'demo-tracks.gp', '&fill=1')
  const play = page.getByTestId('tab-play')
  await expect(play).toBeEnabled({ timeout: 30_000 })
  const desk = page.getByTestId('tab-desk')
  const panel = page.getByTestId('tab-tracks')

  // Les flèches vont à la dernière mesure : la partition défile dans son cadre, pas la page
  for (let i = 0; i < 15; i++) await page.keyboard.press('ArrowRight')
  await expect(page.getByTestId('tab-bar')).toHaveText('Mesure 16 sur 16')
  await expect.poll(() => desk.evaluate((d) => d.scrollTop)).toBeGreaterThan(0)
  expect(await page.evaluate(() => window.scrollY)).toBe(0)
  await expect(play).toBeInViewport()
  await expect(panel).toBeInViewport()
  const box = (await panel.boundingBox())!
  expect(box.y + box.height).toBeLessThanOrEqual(701)

  // Espace lance et arrête la lecture, L boucle
  await page.keyboard.press('Space')
  await expect(play).toHaveAccessibleName('Mettre en pause')
  await page.keyboard.press('Space')
  await expect(play).toHaveAccessibleName('Lire la tablature')
  await page.keyboard.press('l')
  await expect(page.getByTestId('tab-loop')).toBeChecked()
  expect(violations).toEqual([])
  await page.close()
})

test('#33 : boucle par numéros de mesures, et réglages repliés sur téléphone', async ({ browser }) => {
  const page = await browser.newPage({ viewport: { width: 1280, height: 700 } })
  const violations = await open(page, 'demo-tracks.gp', '&fill=1')
  await expect(page.getByTestId('tab-play')).toBeEnabled({ timeout: 30_000 })
  await page.getByTestId('tab-loop-from').fill('3')
  await page.getByTestId('tab-loop-from').press('Tab')
  await page.getByTestId('tab-loop-to').fill('4')
  await page.getByTestId('tab-loop-to').press('Tab')
  await expect(page.locator('.at-selection div').first()).toBeVisible()      // alphaTab montre la sélection
  await expect(page.getByTestId('tab-loop-clear')).toBeVisible()
  expect(violations).toEqual([])
  await page.close()

  const phone = await browser.newPage({ viewport: { width: 360, height: 780 }, isMobile: true, hasTouch: true })
  await open(phone, 'demo-tracks.gp', '&fill=1')
  await expect(phone.getByTestId('tab-play')).toBeEnabled({ timeout: 30_000 })
  await expect(phone.getByTestId('tab-settings')).toBeHidden()
  await phone.getByTestId('tab-settings-toggle').click()
  await expect(phone.getByTestId('tab-zoom')).toBeVisible()
  const overflow = await phone.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth)
  expect(overflow).toBeLessThanOrEqual(0)
  await phone.close()
})
