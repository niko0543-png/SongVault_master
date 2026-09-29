const dateFormatter = new Intl.DateTimeFormat('fr-FR', { dateStyle: 'medium', timeStyle: 'short' })
const numberFormatter = new Intl.NumberFormat('fr-FR', { maximumFractionDigits: 1 })
const units = ['o', 'Ko', 'Mo', 'Go'] as const

export function formatDate(iso: string): string {
  return dateFormatter.format(new Date(iso))
}

export function formatFileSize(bytes: number): string {
  let value = bytes
  let unit = 0
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024
    unit++
  }
  return `${numberFormatter.format(value)} ${units[unit] ?? 'o'}`
}
