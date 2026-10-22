import { LayoutMode, type json, type model } from '@coderline/alphatab'

/**
 * #31 : rendu proche de Guitar Pro (ADR 0037). alphaTab dessine déjà « à la Guitar Pro » (NotationMode.GuitarPro) ;
 * ces réglages ajoutent la mise en page enregistrée dans le fichier, la page blanche, le zoom et le choix des portées.
 */

/** Zooms proposés, comme le menu de Guitar Pro ; 1 = 100 %. */
export const ZOOMS = [0.75, 0.9, 1, 1.1, 1.25, 1.5] as const

/**
 * Au-dessous de cette largeur (celle du menu de #14), la mise en page du fichier (souvent 4 mesures par ligne)
 * serait illisible : alphaTab range alors les mesures lui-même.
 */
export const WIDE_SCREEN = '(min-width: 720px)'

/** « Parchment » respecte les mesures par ligne et leurs largeurs enregistrées par Guitar Pro 6 à 8. */
export function layoutFor(wide: boolean): LayoutMode {
  return wide ? LayoutMode.Parchment : LayoutMode.Page
}

/** Couleurs et polices proches de Guitar Pro : lignes plus foncées que celles d'alphaTab, chiffres de tablature nets. */
export const GUITAR_PRO_RESOURCES: json.RenderingResourcesJson = {
  staffLineColor: '#6b6b6b',
  barSeparatorColor: '#1a1a1a',
  barNumberColor: '#c8102e',
  tablatureFont: '13px Arial, Helvetica, sans-serif',
}

/** Portées montrées : celles du fichier, ou un choix appliqué à toutes les pistes à cordes. */
export type StaffView = 'file' | 'both' | 'tab' | 'score'
export const STAFF_VIEWS: StaffView[] = ['file', 'both', 'tab', 'score']

type Shown = { score: boolean; tab: boolean }

/** Note ce que le fichier montre, pour pouvoir y revenir. */
export function fileStaves(score: model.Score): Map<model.Staff, Shown> {
  const shown = new Map<model.Staff, Shown>()
  for (const track of score.tracks) {
    for (const staff of track.staves) shown.set(staff, { score: staff.showStandardNotation, tab: staff.showTablature })
  }
  return shown
}

/**
 * Change les portées de la partition déjà lue (jamais le fichier). Une piste sans cordes (batterie, piano)
 * n'a pas de tablature : elle garde sa portée quel que soit le choix.
 */
export function applyStaffView(score: model.Score, view: StaffView, original: Map<model.Staff, Shown>) {
  for (const track of score.tracks) {
    for (const staff of track.staves) {
      const fromFile = original.get(staff) ?? { score: staff.showStandardNotation, tab: staff.showTablature }
      const next = view === 'file' || !staff.isStringed
        ? fromFile
        : { score: view !== 'tab', tab: view !== 'score' }
      staff.showStandardNotation = next.score
      staff.showTablature = next.tab
    }
  }
}