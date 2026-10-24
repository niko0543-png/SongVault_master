# Décisions d'architecture (ADR)

Chaque fichier documente **une** décision : contexte, décision, alternatives écartées,
conséquences. Un ADR n'est jamais réécrit : si une décision change, un nouvel ADR remplace
l'ancien, qui passe au statut « remplacé par ADR NNNN ».

| N° | Décision | Statut |
|---|---|---|
| [0001](0001-monolithe-modulaire.md) | Un monolithe modulaire plutôt que des microservices | accepté |
| [0002](0002-fichiers-hors-base.md) | Les fichiers sont stockés hors de la base de données | accepté |
| [0003](0003-numerotation-des-versions.md) | Numérotation des versions par l'agrégat et concurrence optimiste | accepté |
| [0004](0004-cookies-plutot-que-jwt.md) | Authentification par cookie plutôt que par jeton JWT | accepté |
| [0005](0005-bundle-de-migrations.md) | Migrations appliquées par un bundle dans un conteneur ponctuel | accepté |
| [0006](0006-pas-de-mediatr.md) | Ni MediatR ni repository générique | accepté |
| [0007](0007-groupes-et-membres.md) | Les morceaux appartiennent à un groupe, plus à un utilisateur | accepté |
| [0008](0008-roles-dans-le-groupe.md) | Rôles dans le groupe, vérifiés par le filtre [BandScoped] | accepté |
| [0009](0009-invitations-et-e-mails.md) | Invitations par lien à usage unique et envoi d'e-mails | accepté |
| [0010](0010-compte-complet.md) | Confirmation d'adresse et mot de passe oublié avec les endpoints d'Identity | accepté |
| [0011](0011-rgpd-taches-de-fond.md) | RGPD : tâches de fond en base, export asynchrone et suppression différée | accepté |
| [0012](0012-stockage-objet-et-hebergement.md) | Stockage objet Azure Blob, envoi direct par URL signée, hébergement Azure en France | accepté |
| [0013](0013-sauvegardes-et-reprise.md) | Sauvegardes et reprise : services natifs d'Azure, verrous et exercice trimestriel | accepté |
| [0014](0014-commentaires-horodates.md) | Commentaires horodatés : agrégat à part, ouverts aux Guests, gardés sans auteur | accepté |
| [0015](0015-differences-entre-versions.md) | Différences entre versions calculées dans le navigateur, sans dépendance | accepté |
| [0016](0016-sorties-et-pistes.md) | Sorties et pistes : agrégat Release, liste complète enregistrée en deux passes | accepté 
| [0017](0017-fil-d-activite-et-badges.md) | Fil d'activité et badges : un événement par modification, écrit dans la même transaction | accepté |
| [0018](0018-notifications-par-e-mail.md) | Notifications par e-mail : préparées en arrière-plan à partir du fil d'activité | accepté |
| [0019](0019-liens-de-partage-externes.md) | Liens de partage externes : jeton haché, lecture seule, journal pseudonymisé | accepté |
| [0020](0020-mobile-et-application-installable.md) | Mobile et application installable : CSS adaptatif, service worker écrit à la main, rien de privé en cache | accepté |
| [0021](0021-forme-d-onde-et-apercu-audio.md) | Forme d'onde et aperçu audio : ffmpeg dans une tâche de fond, dessin SVG sans bibliothèque | accepté |
| [0022](0022-e-mails-en-production-avec-brevo.md) | E-mails en production : Brevo, domaine authentifié, réglage gardé d'un déploiement à l'autre | accepté |
| [0023](0023-abonnements-et-quotas.md) | Abonnements et quotas : formules en base, abonnement porté par le groupe, Stripe sans SDK | accepté |
| [0024](0024-analyse-antivirus.md) | Analyse antivirus : ClamAV partout, dans une tâche de fond, fichier illisible avant le verdict, activée en production à la commercialisation | accepté |
| [0025](0025-supervision-en-production.md) | Supervision en production : OpenTelemetry vers Application Insights, alertes par e-mail, page de statut publique | accepté |
| [0026](0026-double-authentification-et-journal-d-audit.md) | Double authentification et journal d'audit : TOTP d'Identity, historique de connexion, journal en ajout seul | accepté |
| [0027](0027-corbeille.md) | Corbeille : suppression en deux temps, restauration 30 jours, purge quotidienne | accepté |
| [0028](0028-setlists.md) | Setlists : liste ordonnée des sorties, durée totale calculée par l'API, feuille imprimée sans bibliothèque | accepté |
| [0029](0029-credits-et-repartition-des-droits.md) | Crédits et répartition des droits : règle des 100 % dans le domaine, ISWC sur le morceau, ISRC sur la version, CSV écrit sans bibliothèque | accepté |
| [0030](0030-pistes-separees-et-melangeur.md) | Pistes séparées : la piste portée par le fichier, mélangeur Web Audio calé sur l'horloge audio | accepté |
| [0031](0031-archive-complete-d-une-sortie.md) | Archive complète d'une sortie : ZIP écrit en flux dans le stockage, téléchargeable 24 h après connexion | accepté |
| [0032](0032-paroles-avec-accords.md) | Paroles avec accords : ChordPro dans le champ paroles, transposition à l'affichage | accepté |
| [0033](0033-interface-multilingue.md) | Interface multilingue : catalogues TypeScript, erreurs de l'API traduites à la sortie | accepté |
| [0034](0034-lecteur-guitar-pro.md) | Lecteur Guitar Pro : alphaTab chargé à la demande, en lecture seule | accepté |
| [0035](0035-comparaison-guitar-pro.md) | Comparaison de deux fichiers Guitar Pro : empreintes de mesures, diff dans un Web Worker | accepté |
| [0036](0036-tablature-calee-sur-la-maquette.md) | Tablature calée sur la maquette : points de synchronisation d'alphaTab, enregistrés par fichier | accepté |
| [0037](0037-rendu-facon-guitar-pro.md) | Rendu de la tablature façon Guitar Pro : réglages d'alphaTab, sans nouvelle bibliothèque | accepté |
| [0038](0038-page-du-lecteur-guitar-pro.md) | Page du lecteur Guitar Pro : un seul bloc en plein écran, piloté au clavier | accepté |
| [0039](0039-confort-du-lecteur-guitar-pro.md) | Confort du lecteur Guitar Pro : boucle par mesures, réglages gardés sur l'appareil | accepté |
| [0040](0040-finitions-du-lecteur-guitar-pro.md) | Finitions du lecteur Guitar Pro : temps, icônes, double-clic, accès direct | accepté |
| [0041](0041-barre-facon-guitar-pro.md) | Barre du lecteur façon Guitar Pro et panneau des pistes redimensionnable | accepté |
| [0042](0042-modifications-guitar-pro-a-cote-du-fichier.md) | Modifications Guitar Pro résumées à côté du fichier, détail dans le lecteur | accepté |
| [0044](0044-sortie-d-un-morceau.md) | Sortie d'un morceau : infos de sortie sur la dernière version Final, liens d'écoute, versions gardées | accepté |
| [0045](0045-onglet-morceaux-et-volumes.md) | Onglet « Morceaux » et réglages de volume (lecteur audio, piste « Général » du lecteur Guitar Pro) | accepté |
| [0043](0043-comparaison-guitar-pro-vert-et-rouge.md) | Comparaison Guitar Pro : ajouté en vert, modifié en orange, retiré en rouge, pistes en bas | accepté |
| [0046](0046-reprise-d-une-version-precedente.md) | Reprendre des éléments d'une version précédente : textes dans les champs, fichiers copiés | accepté |
| [0047](0046-setlist-morceaux-a-cocher-et-commentaires.md) | Setlists : morceaux existants à cocher et commentaires entre les morceaux | accepté |
| [0048](0048-suppression-d-un-groupe.md) | Suppression d'un groupe par son Owner : immédiate, nom recopié, abonnement résilié d'abord | accepté |
