SongVault — API et outils pour gérer une bibliothèque de chansons.

Prérequis
- .NET 10 SDK

Lancement
dotnet run --project src/SongVault.Api

## Démarrer avec Docker

Prérequis : Docker Desktop (moteur Linux).

```powershell
Copy-Item .env.example .env     # puis définir MSSQL_SA_PASSWORD
docker compose up -d --build
```

Application : http://localhost:8080 (première exécution : 1 à 3 minutes).

| Service | Rôle | Port (dev) |
|---|---|---|
| web | nginx : front + relais /api | 8080 |
| api | ASP.NET Core 10 | 5081 (override) |
| db | SQL Server 2022 | 1433 (override) |
| migrator | applique les migrations EF Core puis s'arrête | — |

Arrêt : `docker compose down` (les données sont conservées).
⚠️ `docker compose down -v` supprime la base ET les fichiers.