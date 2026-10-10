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