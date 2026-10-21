import { intlLocale, labels, t } from '@/shared/i18n'
import type { Plan, SubscriptionStatus } from './types'

/** 600 → "6,00 € par mois" ; 0 → "Gratuit" */
export function formatMonthlyPrice(cents: number): string {
  if (cents === 0) return t('billing.format.free')
  const price = new Intl.NumberFormat(intlLocale(), { style: 'currency', currency: 'EUR' }).format(cents / 100)
  return t('billing.format.perMonth', { price })
}

/** Ligne « groupes » d'une formule. */
export function describeBands(plan: Plan): string {
  if (plan.maxBandsPerOwner === null) return t('billing.format.unlimitedBands')
  return plan.maxBandsPerOwner === 1
    ? t('billing.format.oneBand')
    : t('billing.format.bands', { count: plan.maxBandsPerOwner })
}

export const statusLabels = labels<SubscriptionStatus>({
  Active: 'billing.status.Active',
  Trialing: 'billing.status.Trialing',
  PastDue: 'billing.status.PastDue',
  Incomplete: 'billing.status.Incomplete',
  IncompleteExpired: 'billing.status.IncompleteExpired',
  Canceled: 'billing.status.Canceled',
  Unpaid: 'billing.status.Unpaid',
  Paused: 'billing.status.Paused',
})

/** Part du quota utilisée, bornée à 0-1 pour la jauge (au-delà après une baisse de formule). */
export function usageRatio(usedBytes: number, quotaBytes: number): number {
  return quotaBytes <= 0 ? 1 : Math.min(1, Math.max(0, usedBytes / quotaBytes))
}