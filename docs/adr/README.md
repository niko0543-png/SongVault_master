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