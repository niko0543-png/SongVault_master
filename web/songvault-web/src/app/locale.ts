import type { Widen } from '@/shared/i18n/types'

/** #26 : en-tête, navigation et pied de page (AppLayout.vue). */
const fr = {
  skipLink: 'Aller au contenu',
  menu: { open: 'Menu', close: 'Fermer' },
  nav: {
    label: 'Navigation principale',
    songs: 'Morceaux',
    releases: 'Sorties',
    setlists: 'Setlists',
    record: 'Enregistrer',
    activity: 'Activité',
    members: 'Membres',
    trash: 'Corbeille',
    billing: 'Facturation',
    audit: 'Journal',
    account: 'Mon compte',
    logout: 'Se déconnecter',
  },
  footer: {
    legalNotice: 'Mentions légales',
    privacy: 'Confidentialité',
    plans: 'Formules',
    termsOfSale: 'CGV',
    status: 'État du service',
  },
}

const en: Widen<typeof fr> = {
  skipLink: 'Skip to content',
  menu: { open: 'Menu', close: 'Close' },
  nav: {
    label: 'Main navigation',
    songs: 'Songs',
    releases: 'Releases',
    setlists: 'Setlists',
    record: 'Record',
    activity: 'Activity',
    members: 'Members',
    trash: 'Trash',
    billing: 'Billing',
    audit: 'Audit log',
    account: 'My account',
    logout: 'Sign out',
  },
  footer: {
    legalNotice: 'Legal notice',
    privacy: 'Privacy',
    plans: 'Plans',
    termsOfSale: 'Terms of sale',
    status: 'Service status',
  },
}

export const layoutMessages = { fr, en }