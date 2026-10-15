// #14 : génère les PNG de l'application installable à partir de public/icons/icon.svg.
// Utilise Chromium de Playwright (déjà là pour les tests e2e) : aucun paquet de plus.
// Lancer depuis web/songvault-web :  node scripts/generate-icons.mjs
import { readFileSync } from 'node:fs'
import { chromium } from '@playwright/test'

const svg = readFileSync(new URL('../public/icons/icon.svg', import.meta.url), 'utf8')
const out = (name) => new URL(`../public/icons/${name}`, import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1')

// maskable : fond plein jusqu'aux bords (le système découpe sa propre forme) ; les autres gardent les coins arrondis
const icons = [
  { name: 'icon-192.png', size: 192, maskable: false },
  { name: 'icon-512.png', size: 512, maskable: false },
  { name: 'icon-maskable-512.png', size: 512, maskable: true },
  { name: 'apple-touch-icon.png', size: 180, maskable: true },   // iOS arrondit lui-même, et refuse la transparence
]

// CHROMIUM_PATH : un Chromium déjà installé ailleurs ; sinon celui de « npx playwright install chromium »
const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_PATH || undefined })
const page = await browser.newPage()
for (const { name, size, maskable } of icons) {
  const source = maskable ? svg.replace('rx="96"', 'rx="0"') : svg
  await page.setViewportSize({ width: size, height: size })
  await page.setContent(`<style>html,body{margin:0;background:transparent}svg{display:block;width:${size}px;height:${size}px}</style>${source}`)
  await page.screenshot({ path: out(name), omitBackground: !maskable })
  console.log(`public/icons/${name} (${size} px)`)
}
await browser.close()