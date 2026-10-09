# Changelog

Format : [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/) — versions : [SemVer](https://semver.org/lang/fr/).

## [Non publié]
### Ajouté
- Groupes : un morceau appartient à un groupe, un utilisateur peut être membre de plusieurs groupes (ADR 0007).
- Sélecteur de groupe dans l'en-tête ; groupe personnel créé automatiquement pour chaque compte.
- Script `scripts/purge-orphan-files.ps1` : retire du volume les fichiers qui ne sont plus référencés.
### Modifié
- Routes de l'API sous `/api/bands/{bandId}/songs…` et du front sous `/b/{bandId}/songs…`.
### Supprimé
- `Song.OwnerId`, remplacé par `Song.BandId`. Les morceaux sans propriétaire valide sont supprimés par la migration `BackfillBands`.

## [0.6.0] - 2026-10-23
### Ajouté
- BPM et tonalité sur les versions (objet valeur `MusicalKey`, contrainte CHECK SQL).
- Recherche par titre/artiste et filtre par statut, synchronisés avec l'URL.
- CI GitHub Actions : backend (dont tests d'intégration SQL Server), frontend, build Docker et test de fumée.
- Dependabot, secret scanning avec push protection, CodeQL.
- Journal d'utilisation critique de l'IA (`docs/ai-usage.md`).

## [0.5.0]
### Ajouté
- Docker Compose (db, migrator, api, web), migrations par bundle EF Core, health checks, sauvegardes.

## [0.4.0]
### Ajouté
- Versions, upload, lecteur audio global, comparaison A/B.

<!-- Complétez 0.1.0 à 0.3.0 à partir de vos tags : git log --oneline v0.2.0..v0.3.0 -->