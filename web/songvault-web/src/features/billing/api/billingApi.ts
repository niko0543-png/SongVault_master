import { request } from '@/shared/api/httpClient'
import { bandPath } from '@/shared/api/bandPath'
import type { Billing, BandUsage, PlanCatalog, Redirect } from '../types'

export const billingApi = {
  plans: () => request<PlanCatalog>('/plans'),
  usage: () => request<BandUsage>(bandPath('/usage')),
  billing: () => request<Billing>(bandPath('/billing')),
  checkout: (planId: string) => request<Redirect>(bandPath('/billing/checkout'), { method: 'POST', body: { planId } }),
  portal: () => request<Redirect>(bandPath('/billing/portal'), { method: 'POST' }),
}