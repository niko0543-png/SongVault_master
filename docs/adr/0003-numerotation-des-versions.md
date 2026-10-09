# ADR 0003 — Numérotation des versions par l'agrégat et concurrence optimiste

- Statut : accepté
- Date : 2026-10-09 (décision prise en semaine 2, consignée a posteriori)

## Contexte
Chaque version d'un morceau porte un numéro (v1, v2…) attribué par le serveur, unique et
sans doublon, y compris quand deux requêtes créent une version au même instant.

## Décision
- Le morceau (`Song`) porte un compteur `LastVersionNumber` ; `Song.AddVersion()`
  l'incrémente. La règle vit dans le domaine, testable sans base.
- Une colonne `rowversion` (propriété fantôme, configurée dans `Infrastructure`) détecte
  toute modification concurrente du morceau : l'`UPDATE` porte
  `WHERE Id = @id AND RowVersion = @ancienneValeur`.
- Un index unique `(SongId, Number)` sert de filet de sécurité en base. Ses violations
  (erreurs SQL 2601 et 2627) sont traduites en `ConcurrencyConflictException`.
- Le handler retente au plus 3 fois (en vidant le suivi des modifications entre deux
  essais), puis renvoie **409 Conflict**.

## Alternatives écartées
- **`MAX(Number) + 1`** : deux lectures simultanées donnent le même numéro.
- **Transaction `SERIALIZABLE`** : risque d'interblocages, verrous plus longs, couplage
  fort au SGBD.
- **`SEQUENCE` SQL** : il en faudrait une par morceau, et une séquence laisse des trous en
  cas d'annulation.
- **Verrou pessimiste (`UPDLOCK`)** : SQL écrit à la main, invisible dans le domaine.

## Conséquences
- Un numéro n'est jamais réutilisé, même après suppression d'une version.
- En cas de forte contention sur un même morceau, certaines requêtes recevront un 409 :
  c'est acceptable pour un usage humain.
- Vérifié par un test d'intégration : 10 créations parallèles contre un vrai SQL Server
  (Testcontainers) donnent les numéros 1 à 10, sans doublon. Le journal « duplicate key »
  produit pendant ce test est attendu.
