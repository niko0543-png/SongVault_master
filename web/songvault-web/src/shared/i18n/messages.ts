import { commonMessages } from './common'
import { layoutMessages } from '@/app/locale'
import { accountMessages } from '@/features/account/locale'
import { activityMessages } from '@/features/activity/locale'
import { auditMessages } from '@/features/audit/locale'
import { authMessages } from '@/features/auth/locale'
import { bandsMessages } from '@/features/bands/locale'
import { billingMessages } from '@/features/billing/locale'
import { chordsMessages } from '@/features/chords/locale'
import { commentsMessages } from '@/features/comments/locale'
import { filesMessages } from '@/features/files/locale'
import { guitarproMessages } from '@/features/guitarpro/locale'
import { invitationsMessages } from '@/features/invitations/locale'
import { legalMessages } from '@/features/legal/locale'
import { notificationsMessages } from '@/features/notifications/locale'
import { playerMessages } from '@/features/player/locale'
import { releasesMessages } from '@/features/releases/locale'
import { rightsMessages } from '@/features/rights/locale'
import { routerMessages } from '@/router/locale'
import { setlistsMessages } from '@/features/setlists/locale'
import { sharedMessages } from '@/shared/locale'
import { sharingMessages } from '@/features/sharing/locale'
import { songsMessages } from '@/features/songs/locale'
import { statusMessages } from '@/features/status/locale'
import { stemsMessages } from '@/features/stems/locale'
import { trashMessages } from '@/features/trash/locale'
import { versionsMessages } from '@/features/versions/locale'
import type { Widen } from './types'

/** #26 : tous les catalogues, une entrée par fonctionnalité. Écrit par le script de #26 ; une fonctionnalité de plus = une ligne de plus. */
export const fr = {
  common: commonMessages.fr,
  layout: layoutMessages.fr,
  account: accountMessages.fr,
  activity: activityMessages.fr,
  audit: auditMessages.fr,
  auth: authMessages.fr,
  bands: bandsMessages.fr,
  billing: billingMessages.fr,
  chords: chordsMessages.fr,
  comments: commentsMessages.fr,
  files: filesMessages.fr,
guitarpro: guitarproMessages.fr,
  invitations: invitationsMessages.fr,
  legal: legalMessages.fr,
  notifications: notificationsMessages.fr,
  player: playerMessages.fr,
  releases: releasesMessages.fr,
  rights: rightsMessages.fr,
  router: routerMessages.fr,
  setlists: setlistsMessages.fr,
  shared: sharedMessages.fr,
  sharing: sharingMessages.fr,
  songs: songsMessages.fr,
  status: statusMessages.fr,
  stems: stemsMessages.fr,
  trash: trashMessages.fr,
  versions: versionsMessages.fr,
}

export const en: Widen<typeof fr> = {
  common: commonMessages.en,
  layout: layoutMessages.en,
  account: accountMessages.en,
  activity: activityMessages.en,
  audit: auditMessages.en,
  auth: authMessages.en,
  bands: bandsMessages.en,
  billing: billingMessages.en,
  chords: chordsMessages.en,
  comments: commentsMessages.en,
  files: filesMessages.en,
  guitarpro: guitarproMessages.en,
  invitations: invitationsMessages.en,
  legal: legalMessages.en,
  notifications: notificationsMessages.en,
  player: playerMessages.en,
  releases: releasesMessages.en,
  rights: rightsMessages.en,
  router: routerMessages.en,
  setlists: setlistsMessages.en,
  shared: sharedMessages.en,
  sharing: sharingMessages.en,
  songs: songsMessages.en,
  status: statusMessages.en,
  stems: stemsMessages.en,
  trash: trashMessages.en,
  versions: versionsMessages.en,
}
