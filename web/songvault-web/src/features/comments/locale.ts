import type { Widen } from '@/shared/i18n/types'

/** #26 : textes de src/features/comments (ADR 0033). Le français fait référence ; l'anglais a les mêmes clés. */
const fr = {
  entry: {
    you: 'Vous',
    deletedAccount: 'Compte supprimé',
    edited: ' · modifié',
    editLabel: 'Modifier le commentaire',
    edit: 'Modifier',
  },
  panel: {
    deleteTitle: 'Supprimer ce commentaire ?',
    deleteMessage: 'Le commentaire sera supprimé définitivement.',
    newComment: 'Nouveau commentaire',
    placeholder: 'ex. la basse décale au refrain',
    mentionHelp: 'Pour prévenir un membre par e-mail, écrivez @ suivi de son adresse (ex. @ana@exemple.fr).',
    commentAt: 'Commenter à {currentTime}',
    at: ' À {position}',
    removePosition: 'Retirer la position',
    listenHelp: 'Écoutez ce fichier pour commenter un instant précis.',
    publish: 'Publier',
    empty: 'Aucun commentaire sur ce fichier.',
    listenFrom: 'Écouter à partir de {positionMs}',
    general: 'Général',
    resolved: ' Résolu',
    reply: 'Répondre',
    reopen: 'Rouvrir',
    resolve: 'Résoudre',
    yourReply: 'Votre réponse',
    sendReply: 'Envoyer la réponse',
    deleteWithReplies: {
      one: 'Le commentaire et sa réponse seront supprimés.',
      other: 'Le commentaire et ses {count} réponses seront supprimés.',
    },
    resolvedBy: 'par {email}',
  },
}

const en: Widen<typeof fr> = {
  entry: {
    you: 'You',
    deletedAccount: 'Deleted account',
    edited: ' · edited',
    editLabel: 'Edit the comment',
    edit: 'Edit',
  },
  panel: {
    deleteTitle: 'Delete this comment?',
    deleteMessage: 'The comment will be deleted for good.',
    newComment: 'New comment',
    placeholder: 'e.g. the bass is off in the chorus',
    mentionHelp: 'To notify a member by e-mail, write @ followed by their address (e.g. @ana@example.com).',
    commentAt: 'Comment at {currentTime}',
    at: ' At {position}',
    removePosition: 'Remove the position',
    listenHelp: 'Listen to this file to comment on a precise moment.',
    publish: 'Post',
    empty: 'No comments on this file.',
    listenFrom: 'Listen from {positionMs}',
    general: 'General',
    resolved: ' Resolved',
    reply: 'Reply',
    reopen: 'Reopen',
    resolve: 'Resolve',
    yourReply: 'Your reply',
    sendReply: 'Send the reply',
    deleteWithReplies: {
      one: 'The comment and its reply will be deleted.',
      other: 'The comment and its {count} replies will be deleted.',
    },
    resolvedBy: 'by {email}',
  },
}

export const commentsMessages = { fr, en }
