import { MAX_CREDITS, creditRoleLabels, type Credit, type CreditRole } from './types'

/** Une ligne de l'éditeur : la part reste le texte saisi (« 33,33 ») tant qu'on n'enregistre pas. */
export interface CreditRow {
  key: number
  name: string
  role: CreditRole
  share: string
}

let nextKey = 0

export function newRow(role: CreditRole = 'Author', share = ''): CreditRow {
  return { key: ++nextKey, name: '', role, share }
}

export function toRows(credits: Credit[]): CreditRow[] {
  return credits.map((c) => ({ key: ++nextKey, name: c.name, role: c.role, share: formatShareInput(c.sharePercent) }))
}

/** « 33,33 », « 33.33 » ou « 33,33 % » → 33.33 ; autre chose → null. */
export function parseShare(text: string): number | null {
  const clean = text.replace('%', '').replace(',', '.').trim()
  if (!/^\d+(\.\d+)?$/.test(clean)) return null
  return Number(clean)
}

/** 33.33 → « 33,33 », pour la saisie. */
export function formatShareInput(value: number): string {
  return String(value).replace('.', ',')
}

/** Les parts en centièmes de point : 33,33 + 33,33 + 33,34 fait 10 000, pas 100,00000000000001. */
export function toCents(value: number): number {
  return Math.round(value * 100)
}

export function totalCents(rows: CreditRow[]): number {
  return rows.reduce((sum, row) => sum + toCents(parseShare(row.share) ?? 0), 0)
}

const percent = new Intl.NumberFormat('fr-FR', { maximumFractionDigits: 2 })

/** 9900 → « 99 % », 3333 → « 33,33 % ». */
export function formatPercent(cents: number): string {
  return `${percent.format(cents / 100)} %`
}

/**
 * Ce qui empêche d'enregistrer, ou null. Mêmes règles que Song.SetRights côté serveur : liste vide permise,
 * sinon un nom par ligne, des parts de 0,01 à 100 avec deux décimales au plus, et un total de 100 % exactement.
 */
export function creditsError(rows: CreditRow[]): string | null {
  if (rows.length === 0) return null
  if (rows.length > MAX_CREDITS) return `Un morceau a au plus ${MAX_CREDITS} crédits.`
  const seen = new Set<string>()
  for (const row of rows) {
    const name = row.name.trim()
    if (!name) return 'Donnez un nom à chaque ayant droit.'
    const share = parseShare(row.share)
    if (share === null) return `La part de ${name} n'est pas un nombre.`
    if (share <= 0 || share > 100) return `La part de ${name} doit être comprise entre 0,01 et 100 %.`
    if (Math.abs(share * 100 - toCents(share)) > 1e-9) return `La part de ${name} a au plus deux décimales.`
    const key = `${name.toLocaleLowerCase('fr-FR')}|${row.role}`
    if (seen.has(key)) return `${name} apparaît deux fois comme ${creditRoleLabels[row.role].toLowerCase()} : regroupez ses parts.`
    seen.add(key)
  }
  const total = totalCents(rows)
  if (total !== 10000) return `Le total fait ${formatPercent(total)} : il doit faire exactement 100 %.`
  return null
}

/** Les lignes au format de l'API ; à n'appeler qu'une fois creditsError à null. */
export function toCredits(rows: CreditRow[]): Credit[] {
  return rows.map((r) => ({ name: r.name.trim(), role: r.role, sharePercent: parseShare(r.share) ?? 0 }))
}

/** Parts égales qui font 100 % : 3 → 33,33 / 33,33 / 33,34 (le reste va à la dernière). */
export function splitEvenly(count: number): number[] {
  if (count <= 0) return []
  const base = Math.floor(10000 / count)
  return Array.from({ length: count }, (_, i) => (i === count - 1 ? 10000 - base * (count - 1) : base) / 100)
}