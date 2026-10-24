import type { Widen } from '@/shared/i18n/types'

/** #26 : textes de src/features/bands (ADR 0033). Le français fait référence ; l'anglais a les mêmes clés. */
const fr = {
  welcome: {
    title: 'Bienvenue sur SongVault',
    intro: 'Pour commencer, créez votre premier groupe : vos morceaux, sorties et setlists y seront rangés, et vous pourrez y inviter vos musiciens.',
    placeholder: 'Ex. : Les Nocturnes',
    create: 'Créer mon groupe',
    invited: "Vous avez été invité dans un groupe ? Ouvrez le lien reçu par e-mail pour le rejoindre : inutile d'en créer un.",
  },
  switcher: {
    band: 'Groupe',
    newBand: 'Nouveau groupe',
    newBandName: 'Nom du nouveau groupe',
    bandName: 'Nom du groupe',
    create: 'Créer',
    seePlans: 'Voir les formules',
    roles: {
      Owner: 'propriétaire',
      Member: 'membre',
      Guest: 'invité',
    },
  },
  members: {
    roles: {
      Owner: 'Propriétaire',
      Member: 'Membre',
      Guest: 'Invité',
    },
    removeTitle: 'Retirer ce membre ?',
    removeMessage: "{email} n'aura plus accès aux morceaux du groupe.",
    remove: 'Retirer',
    removed: '{email} a été retiré du groupe.',
    leaveTitle: 'Quitter ce groupe ?',
    leaveMessage: "Vous n'aurez plus accès aux morceaux de « {name} ».",
    leave: 'Quitter',
    left: 'Vous avez quitté « {name} ».',
    title: 'Membres',
    leaveButton: 'Quitter le groupe',
    joined: 'Arrivé le {joinedAt}',
    roleOf: 'Rôle de {email}',
    you: '(vous)',
  },
}

const en: Widen<typeof fr> = {
  welcome: {
    title: 'Welcome to SongVault',
    intro: 'To get started, create your first band: your songs, releases and setlists live there, and you can invite your musicians to it.',
    placeholder: 'E.g. The Nocturnes',
    create: 'Create my band',
    invited: 'Have you been invited to a band? Open the link from the e-mail to join it: no need to create one.',
  },
  switcher: {
    band: 'Band',
    newBand: 'New band',
    newBandName: 'Name of the new band',
    bandName: 'Band name',
    create: 'Create',
    seePlans: 'See the plans',
    roles: {
      Owner: 'owner',
      Member: 'member',
      Guest: 'guest',
    },
  },
  members: {
    roles: {
      Owner: 'Owner',
      Member: 'Member',
      Guest: 'Guest',
    },
    removeTitle: 'Remove this member?',
    removeMessage: '{email} will no longer have access to the band’s songs.',
    remove: 'Remove',
    removed: '{email} has been removed from the band.',
    leaveTitle: 'Leave this band?',
    leaveMessage: 'You will no longer have access to the songs of “{name}”.',
    leave: 'Leave',
    left: 'You have left “{name}”.',
    title: 'Members',
    leaveButton: 'Leave the band',
    joined: 'Joined on {joinedAt}',
    roleOf: 'Role of {email}',
    you: '(you)',
  },
}

export const bandsMessages = { fr, en }
