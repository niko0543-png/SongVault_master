import type { FilePolicy } from './types'
import { formatFileSize } from '@/shared/utils/format'
import { t } from '@/shared/i18n'

/** Renvoie un message d'erreur, ou null si le fichier est acceptable selon la politique. */
export function validateFile(file: Pick<File, 'name' | 'size'>, policy: FilePolicy): string | null {
  if (file.size === 0) return t('files.validation.empty')

  const dot = file.name.lastIndexOf('.')
  const extension = dot >= 0 ? file.name.slice(dot).toLowerCase() : ''
  if (!policy.allowedExtensions.includes(extension))
    return t('files.validation.badType', { allowedExtensions: policy.allowedExtensions.join(', ') })

  if (file.size > policy.maxSizeBytes)
    return t('files.validation.tooLarge', { size: formatFileSize(file.size), maxSizeBytes: formatFileSize(policy.maxSizeBytes) })

  return null
}
