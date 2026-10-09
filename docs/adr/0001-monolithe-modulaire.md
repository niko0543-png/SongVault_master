# ADR 0001 — Un monolithe modulaire plutôt que des microservices

- Statut : accepté
- Date : 2026-10-09 (décision prise en semaine 1, consignée a posteriori)

## Contexte
SongVault gère un seul domaine (morceaux → versions → fichiers), développé et exploité par
une seule personne, déployé sur une seule machine avec Docker Compose. Le code doit malgré
tout rester lisible et testable, avec des dépendances qui vont dans un seul sens.

## Décision
- Une seule application ASP.NET Core, déployée comme un seul conteneur API.
- Le découpage passe par des **projets** .NET (architecture propre légère) :
  `Domain` ← `Application` ← `Infrastructure`, et `Api` qui assemble le tout.
- Le domaine ne référence aucun paquet technique ; les abstractions (`ISongRepository`,
  `IUnitOfWork`, `IFileStorageService`, `ICurrentUser`) sont déclarées dans `Application`
  et implémentées dans `Infrastructure`.
- Le compilateur garantit les frontières : `Domain` ne peut pas appeler EF Core, car il ne
  le référence pas.

## Alternatives écartées
- **Microservices** (un service par agrégat) : réseau, sérialisation, transactions
  distribuées, déploiements multiples, observabilité répartie. Tout ce coût sans aucun des
  bénéfices (équipes indépendantes, mise à l'échelle séparée), puisqu'il n'y a ni plusieurs
  équipes ni charge à répartir.
- **Un seul projet sans couches** : plus rapide au départ, mais rien n'empêche un contrôleur
  d'écrire du SQL ou une entité de dépendre d'EF Core. Les règles métier deviennent
  difficiles à tester sans base.
- **Architecture propre « complète »** (CQRS, médiateur, un projet par cas d'usage) :
  cérémonie disproportionnée pour une dizaine de cas d'usage (voir ADR 0006).

## Conséquences
- Un seul déploiement, une seule base, des transactions locales simples.
- Les tests de domaine s'exécutent en mémoire, sans base ni conteneur.
- Si un module devait un jour être extrait (par exemple le stockage des fichiers), sa
  frontière existe déjà sous forme d'interface.
- Discipline nécessaire : une référence de projet ajoutée par erreur casserait le sens des
  dépendances. Elle se voit en revue de code dans le `.csproj`.
