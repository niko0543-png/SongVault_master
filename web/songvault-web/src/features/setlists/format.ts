import { intlLocale, t } from '@/shared/i18n'

/** "2026-11-28" → "samedi 28 novembre 2026" ; null → "Sans date" */
export function formatSetlistDate(date: string | null): string {
  if (!date) return t('setlists.format.noDate')
  return new Intl.DateTimeFormat(intlLocale(), { dateStyle: 'full', timeZone: 'UTC' }).format(new Date(`${date}T00:00:00Z`))
}

/** Durée d'un morceau : 187431 → "3:07" ; null → "" */
export function formatDuration(ms: number | null): string {
  if (ms === null) return ''
  const seconds = Math.round(ms / 1000)
  return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`
}

/** Durée totale : 2537000 → "42 min 17 s" ; 3725000 → "1 h 02 min 05 s" */
export function formatTotal(ms: number): string {
  const seconds = Math.round(ms / 1000)
  const h = Math.floor(seconds / 3600)
  const min = Math.floor((seconds % 3600) / 60)
  const s = String(seconds % 60).padStart(2, '0')
  return h > 0 ? t('setlists.format.hms', { h, min: String(min).padStart(2, '0'), s }) : t('setlists.format.ms', { min, s })
}

/**
 * Saisie d'une durée : "3:45" ou "4" (minutes) → millisecondes ; vide → null (on reprend la durée audio).
 * undefined : saisie invalide, ou hors de 1 s à 60 min (les limites de l'API).
 */
export function parseDuration(text: string): number | null | undefined {
  const value = text.trim()
  if (value === '') return null
  const match = /^(\d{1,2})(?::([0-5]\d))?$/.exec(value)
  if (!match) return undefined
  const ms = (Number(match[1]) * 60 + Number(match[2] ?? 0)) * 1000
  return ms >= 1000 && ms <= 3_600_000 ? ms : undefined
}