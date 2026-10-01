import type { FilePolicy } from './types'
import { formatFileSize } from '@/shared/utils/format'

/** Renvoie un message d'erreur, ou null si le fichier est acceptable selon la politique. */
export function validateFile(file: Pick<File, 'name' | 'size'>, policy: FilePolicy): string | null {
  if (file.size === 0) return 'Le fichier est vide.'

  const dot = file.name.lastIndexOf('.')
  const extension = dot >= 0 ? file.name.slice(dot).toLowerCase() : ''
  if (!policy.allowedExtensions.includes(extension))
    return `Type de fichier non autorisé. Extensions acceptées : ${policy.allowedExtensions.join(', ')}.`

  if (file.size > policy.maxSizeBytes)
    return `Fichier trop volumineux (${formatFileSize(file.size)}). Maximum : ${formatFileSize(policy.maxSizeBytes)}.`

  return null
}
