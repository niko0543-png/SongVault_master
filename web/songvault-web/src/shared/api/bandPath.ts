import { useBandStore } from '@/features/bands/stores/bandStore'

/** '/songs' → '/bands/<groupe actif>/songs' */
export function bandPath(path: string): string {
  const id = useBandStore().activeId
  if (!id) throw new Error('Aucun groupe actif')
  return `/bands/${id}${path}`
}