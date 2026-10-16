import type { Plan, SubscriptionStatus } from './types'

const euros = new Intl.NumberFormat('fr-FR', { style: 'currency', currency: 'EUR' })

/** 600 → "6,00 € par mois" ; 0 → "Gratuit" */
export function formatMonthlyPrice(cents: number): string {
  return cents === 0 ? 'Gratuit' : `${euros.format(cents / 100)} par mois`
}

/** Ligne « groupes » d'une formule. */
export function describeBands(plan: Plan): string {
  if (plan.maxBandsPerOwner === null) return 'Autant de groupes que vous voulez'
  return plan.maxBandsPerOwner === 1
    ? 'Un groupe dont vous êtes propriétaire'
    : `${plan.maxBandsPerOwner} groupes dont vous êtes propriétaire`
}

export const statusLabels: Record<SubscriptionStatus, string> = {
  Active: 'Actif',
  Trialing: "En période d'essai",
  PastDue: 'Paiement en retard',
  Incomplete: 'Paiement à terminer',
  IncompleteExpired: 'Paiement abandonné',
  Canceled: 'Résilié',
  Unpaid: 'Impayé',
  Paused: 'En pause',
}

/** Part du quota utilisée, bornée à 0-1 pour la jauge (au-delà après une baisse de formule). */
export function usageRatio(usedBytes: number, quotaBytes: number): number {
  return quotaBytes <= 0 ? 1 : Math.min(1, Math.max(0, usedBytes / quotaBytes))
}