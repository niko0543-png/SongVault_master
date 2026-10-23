import { request } from '@/shared/api/httpClient'
import { bandPath } from '@/shared/api/bandPath'
import type { ReleaseExport } from '../types'

const path = (releaseId: string, suffix = '') => bandPath(`/releases/${releaseId}/exports${suffix}`)

/** #24 : archive complète d'une sortie (ADR 0031). */
export const releaseExportsApi = {
  /** Dernière archive ; null s'il n'y en a pas, ou si elle a expiré (204). */
  latest: async (releaseId: string) => (await request<ReleaseExport | undefined>(path(releaseId, '/latest'))) ?? null,
  request: (releaseId: string) => request<ReleaseExport>(path(releaseId), { method: 'POST' }),
  /** Lien simple, sans fetch : le navigateur suit la redirection vers le stockage avec le cookie de session. */
  archiveUrl: (releaseId: string, exportId: string) => `/api${path(releaseId, `/${exportId}/archive`)}`,
}