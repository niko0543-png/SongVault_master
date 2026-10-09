# ADR 0002 — Les fichiers sont stockés hors de la base de données

- Statut : accepté
- Date : 2026-10-09 (décision prise en semaine 2, consignée a posteriori)

## Contexte
Chaque version peut porter des fichiers : maquettes audio (jusqu'à 50 Mo), partitions PDF,
images. Il faut les enregistrer, les lire en streaming (lecteur audio avec déplacement dans
le morceau), les supprimer, et sauvegarder l'ensemble.

## Décision
- **SQL Server ne garde que les métadonnées** (`SongFile` : nom d'origine, type, taille,
  `StorageKey`).
- **Le contenu est écrit sur un volume** derrière l'interface `IFileStorageService`
  (`SaveAsync`, `OpenReadAsync`, `DeleteAsync`), implémentée par `LocalFileStorageService`.
- La clé de stockage est générée par le serveur (`Guid.CreateVersion7()` + extension
  autorisée). Elle n'est **jamais** dérivée du nom envoyé par le client, ce qui exclut toute
  traversée de répertoire.
- **Ordre des écritures et compensation :**
  - envoi : fichier d'abord, puis base ; si la base échoue, le fichier est supprimé ;
  - suppression : base d'abord, puis fichier ; un échec laisse au pire un fichier orphelin,
    gênant mais sans danger, jamais une ligne pointant vers un fichier absent.

## Alternatives écartées
- **`VARBINARY(MAX)` dans SQL Server** : base qui gonfle, sauvegardes et restaurations
  lentes, mémoire du serveur sollicitée à chaque lecture, pas de lecture partielle simple
  (en-tête `Range`).
- **`FILESTREAM`** : propre à SQL Server sous Windows, non disponible dans l'image Linux
  utilisée par Docker.
- **Stockage objet (Azure Blob) dès le départ** : dépendance à un service cloud pour
  développer en local, alors qu'un volume suffit pour un seul serveur.

## Conséquences
- Base légère ; fichiers servis en streaming avec `enableRangeProcessing`.
- Deux éléments à sauvegarder ensemble (base **et** volume) : c'est le rôle des scripts de
  sauvegarde.
- Pas de transaction commune base/fichiers : la compensation remplace la transaction, et
  des fichiers orphelins restent possibles (un nettoyage périodique est une évolution
  possible).
- Passer à Azure Blob Storage revient à écrire une seconde implémentation de
  `IFileStorageService`, sans toucher au domaine ni aux handlers.
