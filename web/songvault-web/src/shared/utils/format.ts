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

/** 154.7 → "2:34" */
export function formatTime(seconds: number): string {
  if (!Number.isFinite(seconds) || seconds < 0) return '0:00'
  const total = Math.floor(seconds)
  return `${Math.floor(total / 60)}:${String(total % 60).padStart(2, '0')}`
}
