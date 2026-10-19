import type { AuditItem } from './types'

/** Qui : « Vous », l'adresse de l'auteur, « SongVault » (antivirus) ou « Compte supprimé ». */
export function actorLabel(item: AuditItem, myId: string | null): string {
  if (item.actorId === null) return 'SongVault'
  if (item.actorId === myId) return 'Vous'
  return item.actorEmail ?? 'Compte supprimé'
}

/** Le compte visé : « vous », son adresse, ou « un compte supprimé ». */
function targetLabel(item: AuditItem, myId: string | null): string {
  if (item.targetUserId !== null && item.targetUserId === myId) return 'vous'
  return item.targetEmail ?? 'un compte supprimé'
}

/** « ana@… a supprimé le morceau Nocturne », « Vous avez retiré bob@… du groupe (Member) ». */
export function describe(item: AuditItem, myId: string | null): string {
  const actor = actorLabel(item, myId)
  const has = actor === 'Vous' ? 'avez' : 'a'
  const subject = item.subject ?? ''
  const target = targetLabel(item, myId)
  const did = (text: string) => `${actor} ${has} ${text}`
    const purged = (what: string) => item.actorId === null
    ? `La corbeille a effacé ${what} (30 jours écoulés)`
    : did(`supprimé définitivement ${what}`)

   switch (item.action) {
    // #20 : « supprimé » = mis à la corbeille ; « définitivement » = purgé (par la purge à J+30 : SongVault)
    case 'SongDeleted': return did(`supprimé le morceau ${subject}`)
    case 'FileDeleted': return did(`supprimé le fichier ${subject}`)
    case 'VersionDeleted': return did(`supprimé la version ${subject}`)
    case 'SongRestored': return did(`restauré le morceau ${subject}`)
    case 'VersionRestored': return did(`restauré la version ${subject}`)
    case 'FileRestored': return did(`restauré le fichier ${subject}`)
    case 'SongPurged': return purged(`le morceau ${subject}`)
    case 'VersionPurged': return purged(`la version ${subject}`)
    case 'FilePurged': return purged(`le fichier ${subject}`)
    case 'CommentDeleted':
      return did(`supprimé ${subject === 'Réponse' ? 'une réponse' : 'un commentaire'} de ${target}`)
    case 'ReleaseDeleted': return did(`supprimé la sortie ${subject}`)
        // #22 : details résume ce qui a été enregistré (« 3 crédits, ISWC T-034.524.680-1 »)
    case 'CreditsChanged':
      return did(`modifié les crédits du morceau ${subject}${item.details ? ` (${item.details})` : ''}`)
    case 'ShareLinkCreated': return did(`créé le lien de partage ${subject}`)
    case 'ShareLinkRevoked': return did(`coupé le lien de partage ${subject}`)
    case 'ShareLinkDeleted': return did(`supprimé le lien de partage ${subject}`)
    case 'InvitationCreated': return did(`invité ${subject}`)
    case 'InvitationRevoked': return did(`annulé l'invitation de ${subject}`)
    case 'InvitationAccepted': return actor === 'Vous' ? 'Vous avez rejoint le groupe' : `${actor} a rejoint le groupe`
    case 'MemberRoleChanged': return did(`changé le rôle de ${target}`)
    case 'MemberRemoved': return did(`retiré ${target} du groupe`)
    case 'MemberLeft': return actor === 'Vous' ? 'Vous avez quitté le groupe' : `${actor} a quitté le groupe`
    case 'BandRenamed': return did(`renommé le groupe en ${subject}`)
    case 'FileQuarantined': return `L'antivirus a bloqué le fichier ${subject}`
    default: {
      const unreachable: never = item.action
      return `${actor} : ${String(unreachable)}`
    }
  }
}