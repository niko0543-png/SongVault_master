# Registre des traitements — SongVault

Registre tenu au titre de l'article 30 du RGPD. À mettre à jour dans la même PR que toute
fonctionnalité qui collecte, conserve ou transmet des données personnelles.

- **Responsable du traitement** : [Nom ou raison sociale], [adresse], [e-mail de contact]
- **Délégué à la protection des données** : aucun (non obligatoire à cette taille) ;
  contact pour les demandes : [e-mail de contact]
- **Dernière mise à jour** : 2026-10-14

## 1. Comptes utilisateurs

| | |
|---|---|
| Finalité | Créer un compte, se connecter, sécuriser l'accès (confirmation d'adresse, mot de passe oublié) |
| Base légale | Exécution du contrat (art. 6.1.b) |
| Personnes | Utilisateurs inscrits |
| Données | Adresse e-mail, empreinte du mot de passe (PBKDF2, jamais le mot de passe), état de confirmation, verrouillage, empreinte de sécurité |
| Destinataires | Aucun en dehors du service |
| Sous-traitants | Hébergeur ; Brevo pour les e-mails de confirmation et de réinitialisation |
| Transferts hors UE | Aucun |
| Durée | Tant que le compte existe ; après une demande de suppression, 30 jours puis purge (ADR 0011) |
| Sécurité | Cookie `HttpOnly`, `SameSite=Strict` ; limitation de débit ; liens à durée limitée (24 h, 2 h) ; sessions coupées après un changement de mot de passe |

## 2. Contenus des groupes

| | |
|---|---|
| Finalité | Fournir le service : morceaux, versions, paroles, notes, fichiers audio et partitions, partagés entre les membres d'un groupe |
| Base légale | Exécution du contrat (art. 6.1.b) |
| Personnes | Membres des groupes ; personnes éventuellement citées dans les paroles ou les notes |
| Données | Nom du groupe, rôles et dates d'adhésion, contenus saisis et fichiers déposés |
| Destinataires | Les autres membres du groupe, selon leur rôle (ADR 0008) |
| Sous-traitants | Hébergeur (base et stockage des fichiers) |
| Transferts hors UE | Aucun |
| Durée | Tant que le groupe existe ; un groupe dont le compte supprimé était le seul membre est effacé avec lui |
| Sécurité | Isolation par groupe vérifiée à chaque requête ; fichiers hors base, signature binaire contrôlée |

## 3. Invitations

| | |
|---|---|
| Finalité | Inviter une personne à rejoindre un groupe |
| Base légale | Intérêt légitime du groupe (art. 6.1.f) : l'adresse est saisie par un Owner qui connaît la personne |
| Personnes | Personnes invitées, inscrites ou non |
| Données | Adresse invitée, rôle proposé, dates, auteur de l'invitation ; seule l'empreinte du jeton est gardée |
| Destinataires | Owners du groupe |
| Sous-traitants | Brevo (envoi de l'e-mail d'invitation) |
| Transferts hors UE | Aucun |
| Durée | Avec l'historique du groupe ; effacée avec le groupe, ou à la purge du compte qui porte cette adresse |
| Sécurité | Lien à usage unique valable 7 jours ; le compte qui accepte doit avoir l'adresse invitée |

## 4. Exercice des droits (export et suppression)

| | |
|---|---|
| Finalité | Remettre ses données à l'utilisateur ; effacer son compte |
| Base légale | Obligation légale (art. 6.1.c, articles 15, 17 et 20) |
| Personnes | Utilisateurs inscrits |
| Données | Archive ZIP de l'export ; date de la demande de suppression (tâche de purge) |
| Destinataires | L'utilisateur lui-même, connecté |
| Sous-traitants | Hébergeur ; Brevo (e-mail « vos données sont prêtes », sans lien de téléchargement) |
| Transferts hors UE | Aucun |
| Durée | Archive : 7 jours ; tâche de purge : jusqu'à son exécution |
| Sécurité | Téléchargement réservé au compte connecté ; mot de passe redemandé avant la suppression |

## 5. Journaux techniques

| | |
|---|---|
| Finalité | Sécurité, diagnostic des erreurs, limitation des abus |
| Base légale | Intérêt légitime (art. 6.1.f) |
| Personnes | Toute personne qui utilise le site |
| Données | Date, requête, code de réponse, erreurs ; adresse IP utilisée en mémoire par la limitation de débit, non journalisée ; en développement seulement (`Email:Provider=Log`), le contenu des e-mails |
| Destinataires | Exploitant du service |
| Sous-traitants | Hébergeur ; supervision (#18) |
| Transferts hors UE | Aucun |
| Durée | 30 jours (rétention de Log Analytics, `infra/main.bicep`) |
| Sécurité | Aucun mot de passe ni jeton dans les journaux ; accès réservé à l'exploitant |

## 7. Commentaires sur l'audio

| | |
|---|---|
| Finalité | Échanger entre membres d'un groupe sur un enregistrement, à un instant précis |
| Base légale | Exécution du contrat (art. 6.1.b) |
| Personnes | Membres des groupes, Guests compris ; personnes éventuellement citées dans un commentaire |
| Données | Texte du commentaire, position dans l'enregistrement, auteur, dates d'écriture, de modification et de résolution |
| Destinataires | Les autres membres du groupe, quel que soit leur rôle (ADR 0014) |
| Sous-traitants | Hébergeur (base de données) |
| Transferts hors UE | Aucun |
| Durée | Tant que le fichier commenté existe ; dans un groupe partagé, gardé sans auteur après la purge du compte |
| Sécurité | Isolation par groupe ; seul l'auteur modifie, l'auteur ou un Owner supprime |

## 8. Notifications par e-mail

| | |
|---|---|
| Finalité | Prévenir un membre de ce qui change dans ses groupes : nouvelle version, commentaire, mention |
| Base légale | Exécution du contrat (art. 6.1.b) ; chaque type se coupe à tout moment |
| Personnes | Membres des groupes, Guests compris |
| Données | Préférences par type ; éléments du résumé en attente (événement, type, date) ; adresse e-mail ; extrait du commentaire dans l'e-mail |
| Destinataires | Le membre prévenu |
| Sous-traitants | Brevo (envoi des e-mails) ; hébergeur |
| Transferts hors UE | Aucun |
| Durée | Préférences : tant que le compte existe. Éléments du résumé : jusqu'à l'envoi du résumé, au plus un jour |
| Sécurité | Préparé hors requête ; destinataire et préférence revérifiés avant l'envoi ; lien de désinscription signé |

## 9. Liens de partage externes

| | |
|---|---|
| Finalité | Faire écouter une version ou une sortie à une personne sans compte, et savoir si le lien a servi |
| Base légale | Intérêt légitime du groupe (art. 6.1.f) : suivre la diffusion de ses enregistrements |
| Personnes | Visiteurs du lien (sans compte) ; membre qui l'a créé |
| Données | Lien : libellé, auteur, dates, mot de passe haché. Journal : date, type d'accès, fichier écouté, empreinte SHA-256 de l'IP salée par lien (jamais l'IP), navigateur (256 caractères au plus) |
| Destinataires | Les Member et Owner du groupe |
| Sous-traitants | Hébergeur |
| Transferts hors UE | Aucun |
| Durée | Tant que le lien existe : effacé quand un membre le supprime, ou avec la version, la sortie ou le groupe |
| Sécurité | Jeton aléatoire dont seule l'empreinte est en base ; lecture seule, limitée à la cible ; révocation immédiate ; essais de mot de passe limités ; IP jamais conservée |

## Sous-traitants

| Sous-traitant | Rôle | Localisation | Contrat de traitement (DPA) | État |
|---|---|---|---|---|
| Microsoft Ireland Operations Ltd (Azure, région France Central) | Base, fichiers, API, journaux | UE (France) | Addendum de protection des données des produits et services Microsoft (DPA), intégré au contrat d'abonnement | accepté avec l'abonnement Azure |
| Brevo (Sendinblue SAS) | Envoi des e-mails (adresse, sujet et contenu de chaque e-mail ; journal d'envoi chez Brevo) | UE (France) | DPA intégré aux conditions générales de Brevo | accepté à l'ouverture du compte, le 16 octobre 2026 ; copie archivée hors du dépôt |
| [Stripe] (#16) | Paiement | UE / États-Unis (clauses contractuelles types) | DPA de Stripe | quand #16 sera fait |
| [Supervision] (#18) | Erreurs et traces | à choisir | à vérifier | quand #18 sera fait |

## Droits des personnes

- **Accès et portabilité** : « Mon compte » → « Vos données », archive ZIP prête en
  arrière-plan.
- **Effacement** : « Mon compte » → « Supprimer mon compte » ; purge à J+30.
- **Rectification** : adresse et mot de passe depuis « Mon compte » ; contenus modifiables
  dans l'application.
- **Autres demandes** (opposition, limitation, réactivation d'un compte supprimé) : par
  e-mail à [e-mail de contact], réponse sous un mois ; procédures dans
  `docs/runbook-docker.md`.

## Ce qui reste après une suppression

- Les invitations envoyées par le compte dans des groupes qui ont d'autres membres : elles
  appartiennent au groupe, l'identifiant de leur auteur ne renvoie plus à personne.
- Ses commentaires dans les groupes partagés : gardés pour que les fils restent lisibles,
  sans auteur (« Compte supprimé »). Il peut les supprimer lui-même avant de partir.
- Les liens de partage qu'il a créés dans les groupes partagés : ils appartiennent au groupe
  et restent actifs, sans auteur, jusqu'à leur expiration ou leur révocation.
- Les sauvegardes, jusqu'à leur rotation : 7 jours pour la base (restauration à un instant
  donné), 14 jours pour les fichiers (suppression réversible), voir
  `docs/runbook-sauvegardes.md`.

## Avant l'ouverture au public

- [ ] Identité de l'éditeur et contact dans `web/songvault-web/src/features/legal/publisher.ts`
- [x] Hébergeur choisi (#6) et ajouté aux mentions légales et à ce registre
- [ ] DPA de l'hébergeur accepté, copie archivée
- [x] DPA de Brevo accepté, copie archivée (#27)
- [ ] Durées des journaux et des sauvegardes fixées (#6, #7) et reportées ici et dans la politique
- [ ] Mentions légales et politique de confidentialité relues par un professionnel