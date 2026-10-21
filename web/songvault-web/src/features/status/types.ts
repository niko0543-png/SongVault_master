import { labels } from '@/shared/i18n'

/** État renvoyé par GET /api/status (#18). */
export type ServiceState = 'ok' | 'degraded' | 'down'

export interface ComponentStatus {
  name: string
  status: ServiceState
}

export interface StatusReport {
  status: ServiceState
  checkedAt: string
  components: ComponentStatus[]
}

export const STATE_LABELS = labels<ServiceState>({
  ok: 'status.states.ok',
  degraded: 'status.states.degraded',
  down: 'status.states.down',
})
