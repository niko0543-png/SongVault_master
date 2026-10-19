import { request } from '@/shared/api/httpClient'
import { bandPath } from '@/shared/api/bandPath'
import type { AuditPage } from '../types'

export const auditApi = {
  /** Une page du journal, du plus récent au plus ancien ; cursor = nextCursor de la page précédente. */
  list: (cursor?: string | null, pageSize = 50) => {
    const query = new URLSearchParams({ pageSize: String(pageSize) })
    if (cursor) query.set('cursor', cursor)
    return request<AuditPage>(bandPath(`/audit?${query}`))
  },
}