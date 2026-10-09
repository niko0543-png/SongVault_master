# Architecture de SongVault

## Vue d'ensemble

```mermaid
flowchart LR
    subgraph Docker Compose
        Nginx[web : nginx<br/>front Vue compilé<br/>relais /api]
        Api[api : ASP.NET Core 10]
        Migrator[migrator : efbundle<br/>s'exécute puis s'arrête]
        Db[(db : SQL Server 2022)]
        Files[(volume songvault-files)]
    end
    Browser[Navigateur] -->|"HTTP :8080"| Nginx
    Nginx -->|"/api/*"| Api
    Api --> Db
    Api --> Files
    Migrator --> Db
```

## Dépendances entre projets

```mermaid
flowchart TB
    Api[SongVault.Api] --> Application[SongVault.Application]
    Api --> Infrastructure[SongVault.Infrastructure]
    Infrastructure --> Application
    Infrastructure --> Domain[SongVault.Domain]
    Application --> Domain
```

Domain ne dépend de rien ; les abstractions (`ISongRepository`, `IBandRepository`, `IFileStorageService`, `ICurrentUser`, `IBandContext`) sont dans Application, leurs implémentations dans Infrastructure et Api.

## Modèle métier

```mermaid
classDiagram
    class Band {
        Guid Id
        string Name
        Rename()
    }
    class BandMembership {
        Guid BandId
        string UserId
        BandRole Role
    }
    Band "1" --> "*" BandMembership
    Band "1" --> "*" Song
    class Song {
        Guid Id
        Guid BandId
        string Title
        string? Artist
        int LastVersionNumber
        AddVersion() SongVersion
    }
    class SongVersion {
        Guid Id
        int Number
        string Title
        SongVersionStatus Status
        int? Bpm
        MusicalKey? Key
        string? Lyrics
        AddFile() SongFile
    }
    class SongFile {
        Guid Id
        string OriginalFileName
        string StorageKey
        long SizeBytes
        SongFileType FileType
    }
    class SongVersionStatus {
        <<enumeration>>
        Idea
        Demo
        Arrangement
        Rehearsal
        Studio
        Final
    }
    class SongFileType {
        <<enumeration>>
        Audio
        Tablature
        Document
    }
    Song "1" *-- "0..*" SongVersion
    SongVersion "1" *-- "0..*" SongFile
    SongVersion --> SongVersionStatus
    SongFile --> SongFileType
```

Extensions envisagées hors MVP : `Band` et `Member` (groupes), `Comment` (commentaires horodatés sur l'audio), `Tag`, `Setlist`.

## Création d'une version (numérotation concurrente)

```mermaid
sequenceDiagram
    participant C as Client
    participant H as CreateSongVersionHandler
    participant R as SongRepository
    participant DB as SQL Server
    C->>H: POST /api/bands/{bandId}/songs/{id}/versions
    loop au plus 3 tentatives
        H->>R: GetByIdAsync (filtré par groupe actif)
        R->>DB: SELECT Song (LastVersionNumber, RowVersion)
        H->>H: song.AddVersion() : numéro = LastVersionNumber + 1
        H->>DB: INSERT SongVersion + UPDATE Song WHERE RowVersion = @lue
        alt conflit (RowVersion changée ou index unique)
            DB-->>H: erreur → ConcurrencyConflictException
            H->>H: DiscardChanges, courte attente aléatoire
        else succès
            DB-->>H: OK
        end
    end
    H-->>C: 201 Created (ou 409 après 3 conflits)
```

## Upload d'un fichier

```mermaid
sequenceDiagram
    participant C as Client
    participant N as nginx
    participant H as UploadSongFileHandler
    participant S as IFileStorageService
    participant DB as SQL Server
    C->>N: POST …/files (multipart, ≤ 50 Mo)
    N->>H: relais (rate limiting par utilisateur)
    H->>H: extension, taille, signature (32 premiers octets)
    H->>S: SaveAsync(clé générée, flux)
    H->>DB: INSERT SongFile
    alt échec de la base
        H->>S: DeleteAsync(clé) : compensation
    end
    H-->>C: 201 Created
```