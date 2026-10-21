import { ref } from 'vue'
import { en, fr } from './messages'
import type { Leaves, Params, Plural, Widen } from './types'

/**
 * #26 : traduction de l'interface, sans bibliothèque (ADR 0033). t('songs.list.title') rend le texte dans la
 * langue choisie ; appelé dans un gabarit, il le met à jour quand la langue change (locale est réactif).
 */
export type Locale = 'fr' | 'en'
export type MessageKey = Leaves<typeof fr>
export type { Params, Plural }

/** Les noms de langue restent écrits dans leur langue, comme partout ailleurs. */
export const LOCALES: readonly { code: Locale; label: string }[] = [
  { code: 'fr', label: 'Français' },
  { code: 'en', label: 'English' },
]

const catalogs: Record<Locale, Widen<typeof fr>> = { fr, en }
const STORAGE_KEY = 'songvault.locale'

export function isLocale(value: unknown): value is Locale {
  return value === 'fr' || value === 'en'
}

/** Langue choisie sur cet appareil, sinon celle du navigateur si on la propose, sinon le français. */
function initialLocale(): Locale {
  try {
    const stored = localStorage.getItem(STORAGE_KEY)
    if (isLocale(stored)) return stored
  } catch {
    // stockage bloqué (navigation privée) : on se rabat sur le navigateur
  }
  const preferred = typeof navigator === 'undefined' ? [] : navigator.languages ?? [navigator.language]
  for (const tag of preferred) {
    const code = tag.slice(0, 2).toLowerCase()
    if (isLocale(code)) return code
  }
  return 'fr'
}

export const locale = ref<Locale>(initialLocale())
if (typeof document !== 'undefined') document.documentElement.lang = locale.value

export function setLocale(next: Locale) {
  locale.value = next
  try {
    localStorage.setItem(STORAGE_KEY, next)
  } catch {
    // tant pis : la langue vaut pour cette visite
  }
  if (typeof document !== 'undefined') document.documentElement.lang = next
}

/** Code de langue pour Intl (dates, nombres, monnaies) : jour avant mois en anglais aussi. */
export function intlLocale(value: Locale = locale.value): string {
  return value === 'en' ? 'en-GB' : 'fr-FR'
}

type Node = string | Plural | { [key: string]: Node }

function lookup(catalog: unknown, key: string): string | Plural | undefined {
  let node = catalog as Node | undefined
  for (const part of key.split('.')) {
    if (node === undefined || typeof node === 'string') return undefined
    node = (node as Record<string, Node>)[part]
  }
  return typeof node === 'string' || (node && 'other' in node) ? (node as string | Plural) : undefined
}

function pluralForm(value: Plural, count: Params[string]): string {
  const n = Number(count ?? 0)
  if (n === 0 && value.zero !== undefined) return value.zero
  return new Intl.PluralRules(intlLocale()).select(n) === 'one' ? value.one : value.other
}

function interpolate(text: string, params: Params | undefined): string {
  if (!params) return text
  return text.replace(/\{(\w+)\}/g, (whole, name: string) => {
    if (!(name in params)) return whole
    const value = params[name]
    if (value === null || value === undefined) return ''                       // comme {{ }} dans un gabarit
    return typeof value === 'number' ? new Intl.NumberFormat(intlLocale()).format(value) : value
  })
}

/** Le texte d'une clé dans la langue choisie ; s'il manque (impossible si vue-tsc passe), le français. */
export function t(key: MessageKey, params?: Params): string {
  const value = lookup(catalogs[locale.value], key) ?? lookup(catalogs.fr, key)
  if (value === undefined) return key
  return interpolate(typeof value === 'string' ? value : pluralForm(value, params?.count), params)
}

/**
 * Une table de libellés (statut → texte) écrite avec des clés : chaque lecture traduit dans la langue du
 * moment. Les appels gardent leur forme, statusLabels[s] ; dans un gabarit ou un computed, ils suivent la langue.
 */
export function labels<K extends string>(keys: Record<K, MessageKey>): Readonly<Record<K, string>> {
  const out = {} as Record<K, string>
  for (const name of Object.keys(keys) as K[]) Object.defineProperty(out, name, { get: () => t(keys[name]), enumerable: true })
  return out
}