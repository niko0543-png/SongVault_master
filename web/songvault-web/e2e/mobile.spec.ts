import { fileURLToPath } from 'node:url'
import { expect, test, type Page } from '@playwright/test'
import { createSong, createVersion, registerNewUser } from './helpers'

// #14 : un téléphone Android d'entrée de gamme. Les critères sont mesurés à 360 px de large.
test.use({ viewport: { width: 360, height: 780 }, hasTouch: true, isMobile: true })

const demoMp3 = fileURLToPath(new URL('./fixtures/demo.mp3', import.meta.url))

/** Critère « aucun défilement horizontal à 360 px » : la page n'est pas plus large que l'écran. */
async function expectNoHorizontalScroll(page: Page) {
  await page.waitForLoadState('networkidle')
  const { scroll, client } = await page.evaluate(() => ({
    scroll: document.documentElement.scrollWidth,
    client: document.documentElement.clientWidth,
  }))
  expect(scroll, `${page.url()} défile à l'horizontale`).toBeLessThanOrEqual(client)
}

test('à 360 px, aucune page ne défile à l\'horizontale et le lecteur tient sous le pouce', async ({ page }) => {
  test.setTimeout(60_000)                       // #17 : l'analyse antivirus passe avant l'écoute
  await page.goto('/login')
  await expectNoHorizontalScroll(page)
  await page.goto('/confidentialite')
  await expectNoHorizontalScroll(page)

  await registerNewUser(page)
  await expectNoHorizontalScroll(page)
  // Un titre long, sans espace possible pour couper : le pire cas pour la largeur
  await createSong(page, 'Nocturne-pour-une-ville-qui-ne-dort-jamais-vraiment')
  await createVersion(page, 'Mix final avec les chœurs refaits au studio', 1)
  await expectNoHorizontalScroll(page)

  await page.getByRole('link', { name: 'Mix final avec les chœurs refaits au studio' }).click()
  await page.getByLabel('Choisir un fichier').setInputFiles(demoMp3)
  const file = page.getByTestId('file-item').filter({ hasText: 'demo.mp3' })
  await expect(file.getByRole('button', { name: /Écouter/ })).toBeVisible({ timeout: 30_000 })   // #17 : après l'analyse
  await file.getByRole('button', { name: /Écouter/ }).click()
  await expectNoHorizontalScroll(page)

  // Lecteur : entièrement visible, boutons d'au moins 44 px pour le doigt
  const player = page.getByRole('region', { name: 'Lecteur audio' })
  const box = (await player.boundingBox())!
  expect(box.x).toBeGreaterThanOrEqual(0)
  expect(box.x + box.width).toBeLessThanOrEqual(360)
  for (const name of ['Lire', 'Mettre en pause', 'Reculer de 10 secondes', 'Avancer de 10 secondes']) {
    const button = player.getByRole('button', { name, exact: true })
    if (await button.count() === 0) continue                     // Lire ou Pause, selon l'état
    const b = (await button.boundingBox())!
    expect(Math.min(b.width, b.height), name).toBeGreaterThanOrEqual(44)
  }

  // Menu replié : ouvert par le bouton, refermé par la navigation
  const menu = page.getByRole('button', { name: 'Menu' })
  await expect(page.getByRole('link', { name: 'Activité' })).toBeHidden()
  await menu.click()
  await expect(page.getByRole('button', { name: 'Fermer' })).toHaveAttribute('aria-expanded', 'true')
  await page.getByRole('link', { name: 'Activité' }).click()
  await expect(page.getByRole('heading', { level: 1, name: 'Activité' })).toBeVisible()
  await expect(page.getByRole('link', { name: 'Membres' })).toBeHidden()
  await expectNoHorizontalScroll(page)

  for (const link of ['Sorties', 'Membres']) {
    await page.getByRole('button', { name: 'Menu' }).click()
    await page.getByRole('link', { name: link }).click()
    await expect(page.getByRole('heading', { level: 1, name: link })).toBeVisible()
    await expectNoHorizontalScroll(page)
  }
  await page.getByRole('button', { name: 'Menu' }).click()
  await page.getByRole('link', { name: /@test\.local$/ }).click()
  await expect(page.getByRole('heading', { level: 1, name: 'Mon compte' })).toBeVisible()
  await expectNoHorizontalScroll(page)
})

test("l'application est installable : manifeste, icônes et service worker", async ({ page, request }) => {
  const manifest = await request.get('/manifest.webmanifest')
  expect(manifest.ok()).toBe(true)
  expect(manifest.headers()['content-type']).toContain('application/manifest+json')
  const body = (await manifest.json()) as { display: string; icons: { src: string }[] }
  expect(body.display).toBe('standalone')
  for (const icon of [...body.icons.map((i) => i.src), '/icons/apple-touch-icon.png']) {
    const response = await request.get(icon)
    expect(response.headers()['content-type'], icon).toBe('image/png')
  }

  const sw = await request.get('/sw.js')
  expect(sw.headers()['cache-control']).toBe('no-cache')       // une nouvelle version est vue tout de suite

  await page.goto('/login')
  const scriptURL = await page.evaluate(async () => (await navigator.serviceWorker.ready).active?.scriptURL)
  expect(scriptURL).toMatch(/\/sw\.js$/)
})