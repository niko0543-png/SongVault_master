import { describe, expect, it } from 'vitest'
import { creditsError, formatPercent, newRow, parseShare, splitEvenly, toCredits, totalCents, type CreditRow } from '../rules'
import type { CreditRole } from '../types'

function row(name: string, share: string, role: CreditRole = 'Author'): CreditRow {
  return { ...newRow(role, share), name }
}

describe('règles des crédits', () => {
  it('refuse un total à 99 % et dit de combien il est', () => {
    expect(creditsError([row('Alice', '50'), row('Bruno', '49', 'Composer')]))
      .toBe('Le total fait 99 % : il doit faire exactement 100 %.')
  })

  it('accepte 33,33 + 33,33 + 33,34 sans erreur d’arrondi', () => {
    const rows = [row('Alice', '33,33'), row('Bruno', '33.33', 'Composer'), row('Chloé', '33,34 %', 'Arranger')]
    expect(totalCents(rows)).toBe(10000)
    expect(creditsError(rows)).toBeNull()
    expect(toCredits(rows).map((c) => c.sharePercent)).toEqual([33.33, 33.33, 33.34])
  })

  it('accepte une liste vide : les droits ne sont pas encore renseignés', () => {
    expect(creditsError([])).toBeNull()
  })

  it('refuse un nom vide, une part illisible, hors limites ou à trois décimales, et un doublon', () => {
    expect(creditsError([row(' ', '100')])).toBe('Donnez un nom à chaque ayant droit.')
    expect(creditsError([row('Alice', 'cent')])).toBe("La part de Alice n'est pas un nombre.")
    expect(creditsError([row('Alice', '0')])).toBe('La part de Alice doit être comprise entre 0,01 et 100 %.')
    expect(creditsError([row('Alice', '100,5')])).toBe('La part de Alice doit être comprise entre 0,01 et 100 %.')
    expect(creditsError([row('Alice', '33,333'), row('Bruno', '66,667')])).toBe('La part de Alice a au plus deux décimales.')
    expect(creditsError([row('Alice', '50'), row('alice', '50')]))
      .toBe('alice apparaît deux fois comme auteur : regroupez ses parts.')
    expect(creditsError([row('Alice', '50'), row('Alice', '50', 'Composer')])).toBeNull()
  })

  it('lit la virgule et le point, et écrit les pourcentages à la française', () => {
    expect(parseShare('12,5')).toBe(12.5)
    expect(parseShare(' 12.5 % ')).toBe(12.5)
    expect(parseShare('-3')).toBeNull()
    expect(formatPercent(3333)).toBe('33,33 %')
    expect(formatPercent(10000)).toBe('100 %')
  })

  it('répartit à parts égales en gardant 100 % exactement', () => {
    expect(splitEvenly(3)).toEqual([33.33, 33.33, 33.34])
    expect(splitEvenly(4)).toEqual([25, 25, 25, 25])
    expect(splitEvenly(7).reduce((s, x) => s + Math.round(x * 100), 0)).toBe(10000)
  })
})