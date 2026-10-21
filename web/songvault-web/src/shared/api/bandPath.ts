import { useBandStore } from '@/features/bands/stores/bandStore'
import { t } from '@/shared/i18n'

/** '/songs' → '/bands/<groupe actif>/songs' */
export function bandPath(path: string): string {
  const id = useBandStore().activeId
  if (!id) throw new Error(t('shared.api.noBand'))
  return `/bands/${id}${path}`
}