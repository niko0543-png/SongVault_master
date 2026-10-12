import { execFileSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { join, resolve } from 'node:path'
import { expect, type Page } from '@playwright/test'

// Racine du dépôt : les tests e2e se lancent depuis web/songvault-web
const repoRoot = resolve(process.cwd(), '../..')

/** Mot de passe SA : variable d'environnement, sinon le fichier .env de la racine (celui de docker compose). */
function saPassword(): string {
  if (process.env.MSSQL_SA_PASSWORD) return process.env.MSSQL_SA_PASSWORD
  const line = readFileSync(join(repoRoot, '.env'), 'utf8')
    .split(/\r?\n/)
    .find((l) => l.startsWith('MSSQL_SA_PASSWORD='))
  if (!line) throw new Error('MSSQL_SA_PASSWORD introuvable (variable ou fichier .env)')
  return line.slice('MSSQL_SA_PASSWORD='.length).trim()
}

/** Pas de boîte mail en e2e : l'adresse est confirmée directement en base (#4). */
function confirmEmail(email: string) {
  try {
    execFileSync('docker', [
      'compose', 'exec', '-T', 'db', '/opt/mssql-tools18/bin/sqlcmd',
      // -I : QUOTED_IDENTIFIER ON, exigé par SQL Server pour modifier une table qui a un index filtré (AspNetUsers)
      '-S', 'localhost', '-U', 'sa', '-P', saPassword(), '-C', '-I', '-d', 'SongVault', '-b',
      '-Q', `UPDATE AspNetUsers SET EmailConfirmed = 1 WHERE NormalizedEmail = '${email.toUpperCase()}'`,
    ], { cwd: repoRoot, stdio: 'pipe', encoding: 'utf8' })
  } catch (e) {
    // On montre la réponse de sqlcmd, pas la commande : elle contient le mot de passe SA
    const { stdout, stderr } = e as { stdout?: string; stderr?: string }
    throw new Error(`Confirmation en base impossible : ${stdout ?? ''}${stderr ?? ''}`)
  }
}

/** Inscrit un utilisateur unique, confirme son adresse et le connecte : chaque test part d'une bibliothèque vide. */
export async function registerNewUser(page: Page) {
  const email = `e2e-${Date.now()}-${Math.random().toString(36).slice(2, 8)}@test.local`
  const password = 'E2e-Password1!'

  await page.goto('/register')
  await page.getByLabel('E-mail').fill(email)
  await page.getByLabel('Mot de passe', { exact: true }).fill(password)
  await page.getByLabel('Confirmer le mot de passe').fill(password)
  await page.getByRole('button', { name: 'Créer mon compte' }).click()
  await expect(page.getByRole('heading', { name: 'Vérifiez votre boîte mail' })).toBeVisible()

  confirmEmail(email)
  await page.goto('/login')
  await page.getByLabel('E-mail').fill(email)
  await page.getByLabel('Mot de passe').fill(password)
  await page.getByRole('button', { name: 'Se connecter' }).click()
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