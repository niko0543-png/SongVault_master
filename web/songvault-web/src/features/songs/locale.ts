import type { Widen } from '@/shared/i18n/types'

/** #26 : textes de src/features/songs (ADR 0033). Le français fait référence ; l'anglais a les mêmes clés. */
const fr = {
  search: {
    label: 'Rechercher un morceau',
    placeholder: 'Rechercher par titre ou artiste…',
    statusLabel: 'Filtrer par statut',
    allStatuses: 'Tous les statuts',
  },
  badge: {
    new: 'Nouveau',
    updated: 'Modifié',
    newHelp: 'Pas encore ouvert, et modifié par un autre membre',
    updatedHelp: 'Modifié par un autre membre depuis votre dernière visite',
  },
  card: {
    updated: 'Modifié le {updatedAt}',
    openTablature: 'Ouvrir la tablature de {title}',
  },
  form: {
    titleRequired: 'Le titre est obligatoire.',
    maxLength: '{max} caractères maximum.',
    title: 'Titre *',
    artist: 'Artiste',
    description: 'Description',
    sending: 'Envoi…',
  },
  create: {
    title: 'Nouveau morceau',
    submit: 'Créer',
  },
  detail: {
    deleteTitle: 'Supprimer ce morceau ?',
    deleteMessage: '« {title} », ses versions et ses fichiers vont à la corbeille du groupe : on pourra les restaurer pendant 30 jours.',
    deleted: '« {title} » est dans la corbeille.',
    back: '← Tous les morceaux',
    notFound: "Ce morceau n'existe pas ou a été supprimé.",
    edit: 'Modifier',
    dates: 'Créé le {createdAt} · modifié le {updatedAt}',
    versions: 'Versions',
    compare: 'Comparer des versions',
    versionCreated: 'Version v{lastCreated} créée.',
  },
  edit: {
    title: 'Modifier le morceau',
  },
  list: {
    noMatch: 'Aucun morceau ne correspond à la recherche.',
    empty: "Aucun morceau pour l'instant.",
    title: 'Morceaux',
    new: '+ Nouveau morceau',
    clearFilters: 'Effacer les filtres',
    createFirst: 'Créer le premier morceau',
    count: {
      one: '{count} morceau',
      other: '{count} morceaux',
    },
  },
}

const en: Widen<typeof fr> = {
  search: {
    label: 'Search for a song',
    placeholder: 'Search by title or artist…',
    statusLabel: 'Filter by status',
    allStatuses: 'All statuses',
  },
  badge: {
    new: 'New',
    updated: 'Changed',
    newHelp: 'Not opened yet, and changed by another member',
    updatedHelp: 'Changed by another member since your last visit',
  },
  card: {
    updated: 'Changed on {updatedAt}',
    openTablature: 'Open the tablature of {title}',
  },
  form: {
    titleRequired: 'The title is required.',
    maxLength: '{max} characters at most.',
    title: 'Title *',
    artist: 'Artist',
    description: 'Description',
    sending: 'Sending…',
  },
  create: {
    title: 'New song',
    submit: 'Create',
  },
  detail: {
    deleteTitle: 'Delete this song?',
    deleteMessage: '“{title}”, its versions and its files go to the band’s trash: they can be restored for 30 days.',
    deleted: '“{title}” is in the trash.',
    back: '← All songs',
    notFound: 'This song does not exist or has been deleted.',
    edit: 'Edit',
    dates: 'Created on {createdAt} · changed on {updatedAt}',
    versions: 'Versions',
    compare: 'Compare versions',
    versionCreated: 'Version v{lastCreated} created.',
  },
  edit: {
    title: 'Edit the song',
  },
  list: {
    noMatch: 'No song matches the search.',
    empty: 'No songs yet.',
    title: 'Songs',
    new: '+ New song',
    clearFilters: 'Clear the filters',
    createFirst: 'Create the first song',
    count: {
      one: '{count} song',
      other: '{count} songs',
    },
  },
}

export const songsMessages = { fr, en }
