# ADR 0005 — Migrations appliquées par un bundle dans un conteneur ponctuel

- Statut : accepté
- Date : 2026-10-09 (décision prise en semaine 5, consignée a posteriori)

## Contexte
Le schéma évolue par migrations EF Core. Au déploiement avec Docker Compose, la base doit
être à jour **avant** que l'API ne reçoive des requêtes, sans intervention manuelle.

## Décision
- `dotnet ef migrations bundle` produit un exécutable autonome (`efbundle`) dans une étape
  dédiée du Dockerfile.
- Un service Compose `migrator` exécute ce bundle **une fois**, puis s'arrête avec le code 0.
- L'enchaînement est garanti par Compose :
  `db` (healthy) → `migrator` (`service_completed_successfully`) → `api` → `web`.
- L'API n'appelle **jamais** `Database.Migrate()` au démarrage.

## Alternatives écartées
- **`Database.Migrate()` au démarrage de l'API** : avec plusieurs instances, elles
  migreraient en même temps ; l'API garderait en permanence des droits de modification du
  schéma (DDL) ; une migration longue retarderait chaque démarrage.
- **Script SQL idempotent (`migrations script --idempotent`)** : valable, mais demande un
  client SQL dans l'image et un script à générer à chaque version.
- **Migration manuelle avant chaque déploiement** : oubli garanti un jour.

## Conséquences
- Si une migration échoue, le `migrator` sort en erreur et l'API ne démarre pas : jamais de
  code récent sur un schéma ancien.
- À terme, le compte SQL de l'API peut perdre ses droits DDL ; seul le `migrator` en a
  besoin.
- Le bundle n'a pas besoin du SDK .NET à l'exécution, mais la chaîne de connexion doit lui
  être passée explicitement (`--connection`).
