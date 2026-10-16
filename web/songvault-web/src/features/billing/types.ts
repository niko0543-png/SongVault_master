/** Formules et abonnement d'un groupe (#16, ADR 0023). Les limites viennent de l'API, jamais du code. */
export interface Plan {
  id: string
  name: string
  storageQuotaBytes: number
  /** Groupes sur cette formule dont un même compte peut être propriétaire ; null : sans limite. */
  maxBandsPerOwner: number | null
  /** Prix TTC par mois, en centimes ; 0 pour la formule gratuite. */
  monthlyPriceCents: number
}

export interface PlanCatalog {
  /** Faux tant que le paiement en ligne n'est pas ouvert (Billing:Provider None). */
  onlinePayment: boolean
  plans: Plan[]
}

export interface BandUsage {
  planId: string
  planName: string
  usedBytes: number
  quotaBytes: number
}

export type SubscriptionStatus =
  | 'Active' | 'Trialing' | 'PastDue' | 'Incomplete' | 'IncompleteExpired' | 'Canceled' | 'Unpaid' | 'Paused'

export interface Billing {
  usage: BandUsage
  /** null : le groupe n'a jamais eu d'abonnement. */
  status: SubscriptionStatus | null
  currentPeriodEnd: string | null
  cancelAtPeriodEnd: boolean
  /** Formule offerte à la main (sans paiement). */
  isGranted: boolean
  canCheckout: boolean
  canOpenPortal: boolean
  onlinePayment: boolean
}

export interface Redirect {
  url: string
}