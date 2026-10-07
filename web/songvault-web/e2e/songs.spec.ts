import { expect, test } from '@playwright/test'
import { createSong, createVersion, registerNewUser } from './helpers'

test('un musicien crée un morceau et deux versions numérotées par le serveur', async ({ page }) => {
  await registerNewUser(page)
  await createSong(page, 'The Last Goodbye')

  await createVersion(page, 'Idée acoustique', 1)
  await createVersion(page, 'Première répétition', 2)

  const versions = page.getByTestId('version-item')
  await expect(versions).toHaveCount(2)
  await expect(versions.nth(0)).toContainText('v1')
  await expect(versions.nth(0)).toContainText('Idée acoustique')
  await expect(versions.nth(1)).toContainText('v2')
  await expect(versions.nth(1)).toContainText('Première répétition')

  // Persistance : la page rechargée montre toujours les deux versions
  await page.reload()
  await expect(page.getByTestId('version-item')).toHaveCount(2)
})