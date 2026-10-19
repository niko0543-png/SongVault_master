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

export const STATE_LABELS: Record<ServiceState, string> = {
  ok: 'Opérationnel',
  degraded: 'Ralenti',
  down: 'En panne',
}