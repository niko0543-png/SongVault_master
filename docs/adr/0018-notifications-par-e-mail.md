# ADR 0018 — Notifications par e-mail : préparées en arrière-plan à partir du fil d'activité

- Statut : accepté
- Date : 2026-10-14

## Contexte
Le backlog (#12) demande un e-mail pour une nouvelle version, un nouveau commentaire et une
mention, des préférences par membre avec un résumé quotidien, un lien de désinscription dans
chaque e-mail, et aucun envoi pendant la requête HTTP. Les modifications sont déjà décrites
par `ActivityEvents` (ADR 0017), écrits dans la même transaction qu'elles. Les tâches de fond
(ADR 0011) savent réessayer un envoi.

## Décision
- **Trois types** : nouvelle version (`VersionAdded`), commentaire (`CommentAdded`,
  `CommentReplied`), mention. Une mention est « @ » suivi de l'adresse d'un membre du groupe,
  tant que les comptes n'ont pas de nom d'affichage. Le membre mentionné reçoit la mention à la
  place du commentaire ; s'il a coupé les mentions, c'est sa préférence des commentaires qui
  s'applique. Jamais d'e-mail pour ses propres modifications.
- **Préférences par compte**, valables dans tous ses groupes : table `NotificationPreferences`
  (compte, type, mode), mode `Off`, `Immediate` ou `Daily`. Sans ligne, le mode par défaut :
  tout de suite pour les versions et les mentions, au résumé pour les commentaires.
- **Préparation par `NotificationWorker`**, toutes les minutes, hors requête. Il prend les
  événements dont `NotifiedAt` est nul (`UPDATE … OUTPUT` avec `UPDLOCK, READPAST`), pose
  `NotifiedAt`, et dans la même transaction crée une tâche `notification.send` par e-mail
  immédiat, ou une ligne `NotificationDigestItems` par élément de résumé. `READPAST` saute les
  événements pas encore validés : ils sont pris au tour suivant, aucun n'est perdu même si deux
  transactions valident leurs `Id` dans le désordre. Plusieurs instances de l'API se partagent
  le travail sans doublon.
- **Envoi par les tâches de fond** : `notification.send` revérifie que le destinataire est
  toujours membre et que le type est toujours en `Immediate`, puis envoie. Un refus du
  fournisseur fait réessayer la tâche (trois essais).
- **Résumé quotidien** à 7 h, heure de Paris : une tâche `notification.digest`, que le
  worker reprogramme dès qu'il n'y en a plus en attente. Un e-mail par compte, regroupé par
  groupe ; les éléments sont effacés compte par compte une fois l'e-mail parti.
- **Désinscription** : jeton Data Protection (compte et type, ou tous pour le résumé), sans
  expiration, lien vers la page `/unsubscribe` du front qui demande un clic, et en-têtes
  `List-Unsubscribe` et `List-Unsubscribe-Post` (désinscription en un clic, RFC 8058) vers
  `POST /api/notifications/unsubscribe`, ouvert sans connexion et limité comme la connexion.
- **Événements d'avant le déploiement** : la migration leur donne `NotifiedAt = CreatedAt`,
  personne ne reçoit tout l'historique.

## Alternatives écartées
- **Envoi dans le handler**, juste après `SaveChangesAsync` : contraire au critère « jamais dans
  la requête », et un fournisseur lent ralentirait chaque modification.
- **Curseur « dernier `Id` traité »** (prévu par l'ADR 0017) : il faut relire en arrière ou
  attendre avant de traiter, faute de quoi un `Id` validé en retard est sauté. `NotifiedAt`
  avec `READPAST` n'a pas ce problème, au prix d'une colonne modifiable dans `ActivityEvents`.
- **Une tâche de fond par événement**, créée par `ActivityLog` : chaque handler aurait écrit
  une ligne de plus, et les destinataires auraient été calculés trop tôt.
- **Préférences par groupe** : plus fin, mais trois réglages par groupe et par membre ; on
  pourra ajouter un `BandId` facultatif plus tard.
- **Lien de désinscription qui agit à l'ouverture** (`GET`) : les antivirus de messagerie
  ouvrent les liens tout seuls et désinscriraient tout le monde.

## Conséquences
- Une préparation interrompue après sa transaction n'envoie rien en double : les tâches
  `notification.send` sont en base. Un événement préparé n'est jamais repréparé.
- Les e-mails partent dans la minute, pas à l'instant. Avec `Email:Provider=Log`, ils ne
  partent pas du tout : ils sont écrits dans les journaux de l'API.
- `ActivityEvents` n'est plus tout à fait en ajout seul : seule `NotifiedAt` change, une fois.
- Un nouveau type de notification demande une valeur d'enum, une règle dans
  `NotificationRules` et une phrase dans `NotificationEmails`, sans migration.