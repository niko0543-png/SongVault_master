# Registre des traitements — SongVault

Registre tenu au titre de l'article 30 du RGPD. À mettre à jour dans la même PR que toute
fonctionnalité qui collecte, conserve ou transmet des données personnelles.

- **Responsable du traitement** : [Nom ou raison sociale], [adresse], [e-mail de contact]
- **Délégué à la protection des données** : aucun (non obligatoire à cette taille) ;
  contact pour les demandes : [e-mail de contact]
- **Dernière mise à jour** : 2026-10-12

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
| Durée | [6 mois, à confirmer avec l'hébergement (#6)] |
| Sécurité | Aucun mot de passe ni jeton dans les journaux ; accès réservé à l'exploitant |

## Sous-traitants

| Sous-traitant | Rôle | Localisation | Contrat de traitement (DPA) | État |
|---|---|---|---|---|
| [Hébergeur, Azure France Central proposé en #6] | Base, fichiers, API | UE (France) | Conditions de protection des données de Microsoft | à signer avec l'abonnement |
| Brevo (Sendinblue SAS) | Envoi des e-mails | UE (France) | DPA intégré aux conditions générales de Brevo | à accepter à l'ouverture du compte |
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
- Les sauvegardes, jusqu'à leur rotation : [durée fixée en #7].

## Avant l'ouverture au public

- [ ] Identité de l'éditeur et contact dans `web/songvault-web/src/features/legal/publisher.ts`
- [ ] Hébergeur choisi (#6) et ajouté aux mentions légales et à ce registre
- [ ] DPA de l'hébergeur et de Brevo acceptés, copies archivées
- [ ] Durées des journaux et des sauvegardes fixées (#6, #7) et reportées ici et dans la politique
- [ ] Mentions légales et politique de confidentialité relues par un professionnel