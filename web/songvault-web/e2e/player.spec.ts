import { fileURLToPath } from 'node:url'
import { expect, test } from '@playwright/test'
import { createSong, createVersion, registerNewUser } from './helpers'

// Le projet est un module ES : pas de __dirname, on résout le chemin depuis import.meta.url
const demoMp3 = fileURLToPath(new URL('./fixtures/demo.mp3', import.meta.url))

test('un musicien uploade une maquette, la lance, puis se déconnecte', async ({ page }) => {
  await registerNewUser(page)
  await createSong(page, 'Démo E2E')
  await createVersion(page, 'Maquette', 1)

  // Page de la version
  await page.getByRole('link', { name: 'Maquette' }).click()
  await expect(page.getByRole('heading', { name: /v1 — Maquette/ })).toBeVisible()

  // Upload (l'input est masqué : on passe par son libellé)
  await page.getByLabel('Choisir un fichier').setInputFiles(demoMp3)
  const file = page.getByTestId('file-item').filter({ hasText: 'demo.mp3' })
  await expect(file).toBeVisible()

  // Lecture : on vérifie l'état du lecteur, pas le son
  await file.getByRole('button', { name: /Écouter/ }).click()
  const player = page.getByRole('region', { name: 'Lecteur audio' })
  await expect(player).toContainText('Démo E2E — v1')
  await expect(player).toContainText('demo.mp3')

  // Déconnexion : retour à /login, lecteur masqué, pages protégées inaccessibles
  await page.getByRole('button', { name: 'Se déconnecter' }).click()
  await expect(page).toHaveURL(/\/login/)
  await expect(player).toBeHidden()

  await page.goto('/songs')
  await expect(page).toHaveURL(/\/login\?redirect=/)
})