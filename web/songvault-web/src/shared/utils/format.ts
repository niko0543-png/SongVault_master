import { intlLocale, locale, t } from '@/shared/i18n'

// #26 : formats de la langue choisie, recréés seulement quand elle change
let cachedFor = ''
let dateFormatter: Intl.DateTimeFormat
let numberFormatter: Intl.NumberFormat

function formatters() {
  if (cachedFor !== locale.value) {
    dateFormatter = new Intl.DateTimeFormat(intlLocale(), { dateStyle: 'medium', timeStyle: 'short' })
    numberFormatter = new Intl.NumberFormat(intlLocale(), { maximumFractionDigits: 1 })
    cachedFor = locale.value
  }
  return { dateFormatter, numberFormatter }
}

export function formatDate(iso: string): string {
  return formatters().dateFormatter.format(new Date(iso))
}

export function formatFileSize(bytes: number): string {
  const units = [t('common.fileSize.b'), t('common.fileSize.kb'), t('common.fileSize.mb'), t('common.fileSize.gb')]
  let value = bytes
  let unit = 0
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024
    unit++
  }
  return `${formatters().numberFormatter.format(value)} ${units[unit]}`
}

/** 154.7 → "2:34" */
export function formatTime(seconds: number): string {
  if (!Number.isFinite(seconds) || seconds < 0) return '0:00'
  const total = Math.floor(seconds)
  return `${Math.floor(total / 60)}:${String(total % 60).padStart(2, '0')}`
}
