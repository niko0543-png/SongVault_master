import { request } from '@/shared/api/httpClient'
import { bandPath } from '@/shared/api/bandPath'
import type { ActivityPage } from '../types'

export const activityApi = {
  /** Une page du fil, du plus récent au plus ancien ; cursor = nextCursor de la page précédente. */
  list: (cursor?: string | null, pageSize = 20) => {
    const query = new URLSearchParams({ pageSize: String(pageSize) })
    if (cursor) query.set('cursor', cursor)
    return request<ActivityPage>(bandPath(`/activity?${query}`))
  },
}