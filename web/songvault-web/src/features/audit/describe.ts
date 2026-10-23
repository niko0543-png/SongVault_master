import { t, type MessageKey, type Params } from '@/shared/i18n'
import type { AuditItem } from './types'

/** Qui : « Vous », l'adresse de l'auteur, « SongVault » (antivirus) ou « Compte supprimé ». */
export function actorLabel(item: AuditItem, myId: string | null): string {
  if (item.actorId === null) return 'SongVault'
  if (item.actorId === myId) return t('audit.describe.you')
  return item.actorEmail ?? t('audit.describe.deletedAccount')
}

/** Le compte visé : « vous », son adresse, ou « un compte supprimé ». */
function targetLabel(item: AuditItem, myId: string | null): string {
  if (item.targetUserId !== null && item.targetUserId === myId) return t('audit.describe.targetYou')
  return item.targetEmail ?? t('audit.describe.targetDeleted')
}

/** « ana@… a supprimé le morceau Nocturne », « Vous avez retiré bob@… du groupe (Member) ». */
export function describe(item: AuditItem, myId: string | null): string {
  const actor = actorLabel(item, myId)
  const mine = item.actorId !== null && item.actorId === myId
  const subject = item.subject ?? ''
  const target = targetLabel(item, myId)
  const did = (action: MessageKey, params: Params = { subject }) =>
    t(mine ? 'audit.describe.youDid' : 'audit.describe.theyDid', { actor, action: t(action, params) })
  const purged = (whatKey: MessageKey) => {
    const what = t(whatKey, { subject })
    return item.actorId === null ? t('audit.describe.purgedBySystem', { what }) : did('audit.describe.actions.purged', { what })
  }

   switch (item.action) {
    // #20 : « supprimé » = mis à la corbeille ; « définitivement » = purgé (par la purge à J+30 : SongVault)
    case 'SongDeleted': return did('audit.describe.actions.songDeleted')
    case 'FileDeleted': return did('audit.describe.actions.fileDeleted')
    case 'VersionDeleted': return did('audit.describe.actions.versionDeleted')
    case 'SongRestored': return did('audit.describe.actions.songRestored')
    case 'VersionRestored': return did('audit.describe.actions.versionRestored')
    case 'FileRestored': return did('audit.describe.actions.fileRestored')
    case 'SongPurged': return purged('audit.describe.what.song')
    case 'VersionPurged': return purged('audit.describe.what.version')
    case 'FilePurged': return purged('audit.describe.what.file')
    // subject vaut « Réponse » ou « Commentaire » : c'est le serveur qui l'écrit, pas un texte affiché
    case 'CommentDeleted':
      return did(subject === 'Réponse' ? 'audit.describe.actions.replyDeleted' : 'audit.describe.actions.commentDeleted', { target })
    case 'ReleaseDeleted': return did('audit.describe.actions.releaseDeleted')
    // #22 : details résume ce qui a été enregistré (« 3 crédits, ISWC T-034.524.680-1 »)
    case 'CreditsChanged':
      return did('audit.describe.actions.creditsChanged') + (item.details ? ` (${item.details})` : '')
    // #38 : details dit la version et ce qui est parti (« v3 sur « Nocturne » ; 2 versions à la corbeille »)
    case 'SongReleased':
      return did('audit.describe.actions.songReleased') + (item.details ? ` (${item.details})` : '')
    case 'ShareLinkCreated': return did('audit.describe.actions.shareLinkCreated')
    case 'ShareLinkRevoked': return did('audit.describe.actions.shareLinkRevoked')
    case 'ShareLinkDeleted': return did('audit.describe.actions.shareLinkDeleted')
    case 'InvitationCreated': return did('audit.describe.actions.invitationCreated')
    case 'InvitationRevoked': return did('audit.describe.actions.invitationRevoked')
    case 'InvitationAccepted': return mine ? t('audit.describe.joinedYou') : t('audit.describe.joined', { actor })
    case 'MemberRoleChanged': return did('audit.describe.actions.roleChanged', { target })
    case 'MemberRemoved': return did('audit.describe.actions.memberRemoved', { target })
    case 'MemberLeft': return mine ? t('audit.describe.leftYou') : t('audit.describe.left', { actor })
    case 'BandRenamed': return did('audit.describe.actions.bandRenamed')
    case 'FileQuarantined': return t('audit.describe.quarantined', { subject })
    default: {
      const unreachable: never = item.action
      return `${actor} : ${String(unreachable)}`
    }
  }
}