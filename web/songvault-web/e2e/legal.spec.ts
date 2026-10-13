import { expect, test } from '@playwright/test'

test('les pages légales se lisent sans compte, depuis le pied de page', async ({ page }) => {
  await page.goto('/login')

  await page.getByRole('link', { name: 'Confidentialité', exact: true }).click()
  await expect(page).toHaveURL(/\/confidentialite$/)
  await expect(page.getByRole('heading', { level: 1, name: 'Politique de confidentialité' })).toBeVisible()

  await page.getByRole('link', { name: 'Mentions légales', exact: true }).click()
  await expect(page).toHaveURL(/\/mentions-legales$/)
  await expect(page.getByRole('heading', { level: 1, name: 'Mentions légales' })).toBeVisible()
})