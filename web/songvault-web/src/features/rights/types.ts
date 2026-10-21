import { labels } from '@/shared/i18n'

/** Même liste que CreditRole côté C# (#22). */
export type CreditRole = 'Author' | 'Composer' | 'Arranger' | 'Publisher'

export const creditRoleLabels = labels<CreditRole>({
  Author: 'rights.roles.Author',
  Composer: 'rights.roles.Composer',
  Arranger: 'rights.roles.Arranger',
  Publisher: 'rights.roles.Publisher',
})

/** Mêmes limites que le domaine (Credit.NameMaxLength, Song.MaxCredits). */
export const CREDIT_NAME_MAX = 200
export const MAX_CREDITS = 30
/** La saisie d'un code peut garder espaces, points et tirets. */
export const CODE_INPUT_MAX = 30

export interface Credit {
  name: string
  role: CreditRole
  /** De 0,01 à 100, deux décimales au plus */
  sharePercent: number
}

/** Une version et son ISRC en forme lisible (FR-Z03-26-00001) ; null s'il n'est pas renseigné. */
export interface VersionIsrc {
  id: string
  number: number
  title: string
  isrc: string | null
}

/** GET /api/bands/{id}/songs/{songId}/rights */
export interface SongRights {
  songId: string
  songTitle: string
  /** Forme écrite T-034.524.680-1, ou null */
  iswc: string | null
  credits: Credit[]
  totalPercent: number
  versions: VersionIsrc[]
}

/** Corps de PUT …/rights : l'ensemble, qui remplace l'ancien. */
export interface SongRightsInput {
  iswc: string | null
  credits: Credit[]
}