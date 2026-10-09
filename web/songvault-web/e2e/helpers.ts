import { expect, type Page } from '@playwright/test'

/** Inscrit un utilisateur unique : chaque test part d'une bibliothèque vide. */
export async function registerNewUser(page: Page) {
  const email = `e2e-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@test.local`
  const password = 'E2e-Password1!'

  await page.goto('/register')
  await page.getByLabel('E-mail').fill(email)
  await page.getByLabel('Mot de passe', { exact: true }).fill(password)
  await page.getByLabel('Confirmer le mot de passe').fill(password)
  await page.getByRole('button', { name: 'Créer mon compte' }).click()
  await expect(page).toHaveURL(/\/b\/[^/]+\/songs$/)

  return { email, password }
}

/** Crée un morceau depuis la liste et attend sa page de détail. */
export async function createSong(page: Page, title: string) {
  await page.getByRole('link', { name: /Nouveau morceau/ }).click()
  await page.getByLabel('Titre').fill(title)
  await page.getByRole('button', { name: 'Créer', exact: true }).click()
  await expect(page.getByRole('heading', { level: 1, name: title })).toBeVisible()
}

/** Crée une version depuis la page du morceau. */
export async function createVersion(page: Page, title: string, expectedNumber: number) {
  await page.getByLabel('Titre').fill(title)
  await page.getByRole('button', { name: 'Créer la version' }).click()
  await expect(page.getByText(`Version v${expectedNumber} créée.`)).toBeVisible()
}