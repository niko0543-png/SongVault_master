const dayFormatter = new Intl.DateTimeFormat('fr-FR', { dateStyle: 'long', timeZone: 'UTC' })

/** "2026-11-20" → "20 novembre 2026" ; null → "Date à fixer" */
export function formatReleaseDate(date: string | null): string {
  return date ? dayFormatter.format(new Date(`${date}T00:00:00Z`)) : 'Date à fixer'
}