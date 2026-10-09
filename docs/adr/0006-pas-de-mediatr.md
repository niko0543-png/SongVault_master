# ADR 0006 — Ni MediatR ni repository générique

- Statut : accepté
- Date : 2026-10-09 (décision prise en semaine 1, consignée a posteriori)

## Contexte
La couche `Application` contient une dizaine de cas d'usage (créer un morceau, ajouter une
version, envoyer un fichier…). Il faut les isoler des contrôleurs et les tester, sans
cérémonie inutile.

## Décision
- **Un handler par cas d'usage** (`CreateSongHandler`, `UploadSongFileHandler`…), classe
  concrète enregistrée dans l'injection de dépendances et injectée directement dans
  l'action (`[FromServices]`).
- **Un repository par agrégat** (`ISongRepository`), avec des méthodes nommées selon les
  besoins (`GetOwnedAsync`, `ListStorageKeysAsync`…), plus un `IUnitOfWork` pour valider.
- Les lectures renvoient des projections (`AsNoTracking`, `Select`) adaptées à chaque
  écran.

## Alternatives écartées
- **MediatR** : ajoute une indirection (`Send(command)`) qui casse la navigation « Aller à
  la définition » (F12) et repose sur la réflexion. Son intérêt réel, les comportements
  transversaux (journalisation, validation, transactions), n'est pas utilisé ici : la
  validation est faite par `[ApiController]`, les erreurs par `IExceptionHandler`. Depuis
  2025, les nouvelles versions sont aussi distribuées sous double licence, avec une licence
  commerciale payante au-delà de certains seuils.
- **Repository générique `IRepository<T>`** : réexpose `IQueryable` ou des méthodes
  `GetAll`/`Find` sans intention métier ; le `DbContext` d'EF Core est déjà un repository
  et une unité de travail génériques.
- **Injecter le `DbContext` directement dans les handlers** : plus court, mais `Application`
  dépendrait d'EF Core et les handlers ne seraient plus testables avec un faux repository.

## Conséquences
- Code explicite : depuis le contrôleur, F12 mène directement au handler, puis au
  repository.
- Chaque nouveau cas d'usage demande une classe et une ligne d'enregistrement : coût
  accepté.
- Si des comportements transversaux deviennent nécessaires, un décorateur ou un filtre
  d'action suffira avant d'envisager un médiateur.
