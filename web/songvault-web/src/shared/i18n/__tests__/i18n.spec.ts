import { afterEach, describe, expect, it, vi } from 'vitest'
import { en, fr } from '../messages'
import { intlLocale, labels, locale, setLocale, t, type MessageKey } from '..'

/** Chaque texte du catalogue, avec son chemin : « common.actions.cancel » → « Annuler ». */
function leaves(node: unknown, prefix = ''): Map<string, string> {
  const out = new Map<string, string>()
  for (const [key, value] of Object.entries(node as Record<string, unknown>)) {
    if (typeof value === 'string') out.set(prefix + key, value)
    else if (value && typeof value === 'object' && 'other' in value)
      for (const [form, text] of Object.entries(value)) out.set(`${prefix}${key}#${form}`, text as string)
    else for (const [k, v] of leaves(value, `${prefix}${key}.`)) out.set(k, v)
  }
  return out
}
const holes = (text: string) => [...text.matchAll(/\{(\w+)\}/g)].map((m) => m[1]).sort()

afterEach(() => setLocale('fr'))

describe('catalogues fr et en (#26)', () => {
  it('l’anglais a exactement les clés du français, aucune de plus, aucune de moins', () => {
    expect([...leaves(en).keys()].sort()).toEqual([...leaves(fr).keys()].sort())
  })

  it('chaque texte garde les mêmes paramètres dans les deux langues', () => {
    const english = leaves(en)
    const different = [...leaves(fr)].filter(([key, text]) => holes(text).join() !== holes(english.get(key)!).join())
    expect(different.map(([key]) => key)).toEqual([])
  })

  it('aucun texte vide', () => {
    const empty = [...leaves(fr), ...leaves(en)].filter(([, text]) => text.trim() === '')
    expect(empty).toEqual([])
  })
})

describe('t', () => {
  it('rend le texte de la langue choisie et suit le changement de langue', () => {
    expect(t('common.actions.cancel')).toBe('Annuler')
    setLocale('en')
    expect(t('common.actions.cancel')).toBe('Cancel')
  })

  it('remplace les paramètres et écrit les nombres à la façon de la langue', () => {
    expect(t('common.errors.http', { status: 503 })).toBe('Erreur HTTP 503')
    setLocale('en')
    expect(t('common.errors.serverWithReference', { reference: 'abc' })).toContain('Reference: abc')
    expect(t('common.errors.http', { status: 12345 })).toBe('HTTP error 12,345')
  })

  it('une clé inconnue (impossible si vue-tsc passe) se rend telle quelle', () => {
    expect(t('nope.missing' as MessageKey)).toBe('nope.missing')
  })
})

describe('langue', () => {
  it('setLocale la garde dans le navigateur et met à jour <html lang>', () => {
    setLocale('en')
    expect(localStorage.getItem('songvault.locale')).toBe('en')
    expect(document.documentElement.lang).toBe('en')
    expect(intlLocale()).toBe('en-GB')
    setLocale('fr')
    expect(document.documentElement.lang).toBe('fr')
    expect(intlLocale()).toBe('fr-FR')
  })

  it('au premier chargement : la langue gardée, sinon celle du navigateur, sinon le français', async () => {
    const load = async (stored: string | null, languages: string[]) => {
      vi.resetModules()
      if (stored) localStorage.setItem('songvault.locale', stored)
      else localStorage.removeItem('songvault.locale')
      vi.spyOn(navigator, 'languages', 'get').mockReturnValue(languages)
      return (await import('..')).locale.value
    }
    expect(await load('en', ['fr-FR'])).toBe('en')
    expect(await load(null, ['en-US', 'fr'])).toBe('en')
    expect(await load(null, ['de-DE', 'fr-BE'])).toBe('fr')
    expect(await load(null, ['de-DE'])).toBe('fr')
    expect(await load('xx', ['es'])).toBe('fr')
    vi.restoreAllMocks()
    localStorage.removeItem('songvault.locale')
  })

  it('labels : une table de libellés lue dans la langue du moment', () => {
    const table = labels({ cancel: 'common.actions.cancel', close: 'common.actions.close' })
    expect(table.cancel).toBe('Annuler')
    setLocale('en')
    expect(table.cancel).toBe('Cancel')
    expect(Object.values(table)).toEqual(['Cancel', 'Close'])
    expect(locale.value).toBe('en')
  })
})