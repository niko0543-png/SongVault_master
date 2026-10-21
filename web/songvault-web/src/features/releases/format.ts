import { intlLocale, t } from '@/shared/i18n'

/** "2026-11-20" → "20 novembre 2026" ; null → "Date à fixer" */
export function formatReleaseDate(date: string | null): string {
  if (!date) return t('releases.format.noDate')
  return new Intl.DateTimeFormat(intlLocale(), { dateStyle: 'long', timeZone: 'UTC' }).format(new Date(`${date}T00:00:00Z`))
}