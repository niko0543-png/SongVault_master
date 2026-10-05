# Runbook Docker — SongVault

Cette documentation décrit comment gérer, dépanner et maintenir l'application SongVault en environnement Docker.

## Table des matières
1. [Démarrer / arrêter / mettre à jour](#démarrer--arrêter--mettre-à-jour)
2. [Vérifier l'état](#vérifier-létat)
3. [Lire les journaux](#lire-les-journaux)
4. [Sauvegarder / restaurer](#sauvegarder--restaurer)
5. [Repartir de zéro](#repartir-de-zéro)
6. [Incidents fréquents](#incidents-fréquents)
7. [Commandes utiles](#commandes-utiles)

---

## Démarrer / arrêter / mettre à jour

### Démarrer l'application
```powershell
# Démarrer tous les services (API, Base de données, Nginx)
docker compose up -d

# Vérifier que tout démarre correctement
docker compose logs -f
```

### Arrêter l'application
```powershell
# Arrêter les services (volumes conservés)
docker compose down

# Arrêter et supprimer les volumes (données perdues)
docker compose down -v
```

### Redémarrer l'application
```powershell
# Redémarrer tous les services
docker compose restart

# Redémarrer un service spécifique
docker compose restart api
docker compose restart db
```

### Mettre à jour l'application
```powershell
# 1. Reconstruire l'image API
docker compose build api

# 2. Redémarrer avec la nouvelle image
docker compose up -d api

# 3. Vérifier les logs
docker compose logs -f api
```

### Mettre à jour une dépendance
```powershell
# 1. Modifier le code source
# 2. Reconstruire l'image
docker compose build api

# 3. Relancer
docker compose up -d api
```

---

## Vérifier l'état

### État global des services
```powershell
# Voir tous les conteneurs et leur état
docker compose ps -a

# Exemple de sortie:
# NAME                 COMMAND             STATUS              PORTS
# songvault-api        dotnet SongVault... Up 5 minutes        0.0.0.0:8080->8080/tcp
# songvault-db         postgres            Up 6 minutes        5432/tcp
```

### Vérifier la santé de l'API
```powershell
# Endpoint de santé (liveness probe)
curl http://localhost:8080/health/live

# Endpoint de readiness (readiness probe)
curl http://localhost:8080/health/ready

# Réponse attendue en cas de succès:
# HTTP/1.1 200 OK
# {"status":"Healthy"}
```

### Vérifier la santé de la base de données
```powershell
# Se connecter au conteneur DB
docker compose exec db psql -U postgres -d songvault -c "SELECT 1;"

# Réponse attendue:
#  ?column?
# ----------
#         1
```

### Lister les conteneurs en cours d'exécution
```powershell
docker ps

# Voir aussi les conteneurs arrêtés
docker ps -a
```

---

## Lire les journaux

### Journaux de l'API
```powershell
# Lire les dernières lignes
docker compose logs api

# Suivre les logs en temps réel
docker compose logs -f api

# Afficher les 100 dernières lignes
docker compose logs --tail=100 api
```

### Journaux de la base de données
```powershell
# Logs du service DB
docker compose logs db

# Suivre en temps réel
docker compose logs -f db
```

### Journaux du migrator (si applicable)
```powershell
# Logs du service de migration
docker compose logs migrator

# Suivre en temps réel
docker compose logs -f migrator
```

### Tous les journaux
```powershell
# Afficher tous les logs de tous les services
docker compose logs -f

# Exporter les logs dans un fichier
docker compose logs > songvault-logs.txt
```

### Filtrer les logs
```powershell
# Logs des 5 dernières minutes
docker compose logs --since 5m api

# Logs entre deux timestamps
docker compose logs --until 2024-01-15T12:00:00 api
```

---

## Sauvegarder / restaurer

### Sauvegarder la base de données
```powershell
# Dump PostgreSQL complet
docker compose exec db pg_dump -U postgres -d songvault > songvault-backup.sql

# Dump avec compression
docker compose exec db pg_dump -U postgres -d songvault | gzip > songvault-backup.sql.gz
```

### Sauvegarder les fichiers upload
```powershell
# Vérifier le dossier de destination
dir .\data or ls ./data

# Utiliser le script de sauvegarde
.\scripts\backup-files.ps1

# Ou manuellement avec Docker
docker compose cp db:/var/lib/postgresql/data ./backups/db-$(Get-Date -Format 'yyyyMMdd-HHmmss')
```

### Restaurer la base de données
```powershell
# Restaurer depuis un dump SQL
docker compose exec -T db psql -U postgres -d songvault < songvault-backup.sql

# Restaurer depuis un dump compressé
gunzip -c songvault-backup.sql.gz | docker compose exec -T db psql -U postgres -d songvault
```

### Restaurer les fichiers
```powershell
# Copier les fichiers depuis le backup vers le conteneur
docker compose cp .\backups\data\. db:/var/lib/postgresql/data

# Vérifier que la restauration est correcte
docker compose exec db ls -la /var/lib/postgresql/data
```

### Planifier les sauvegardes automatiques
```powershell
# Windows Task Scheduler - créer une tâche planifiée
# Commande: powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\path\to\backup-files.ps1

# Linux - ajouter au crontab
# 0 2 * * * docker compose -f /path/to/docker-compose.yml exec db pg_dump -U postgres -d songvault | gzip > /backups/songvault-$(date +\%Y\%m\%d-\%H\%M\%S).sql.gz
```

---

## Repartir de zéro

### ⚠️ ATTENTION: Cette action supprime TOUS les données

```powershell
# 1. Arrêter et supprimer tous les conteneurs, réseaux et volumes locaux
docker compose down -v

# 2. (Optionnel) Supprimer les images
docker compose down -v --rmi all

# 3. Regénérer les images
docker compose build

# 4. Redémarrer depuis zéro
docker compose up -d

# 5. Vérifier que tout fonctionne
docker compose logs -f
curl http://localhost:8080/health/live
```

### Que se passe-t-il exactement ?
- **Conteneurs** : supprimés
- **Volumes** : supprimés (données de BD perdues)
- **Réseaux** : supprimés
- **Images** : conservées (utilisez `--rmi all` pour les supprimer)
- **Code source** : non affecté (uniquement le temps d'exécution)

### Si vous voulez garder les données
```powershell
# Arrêter sans supprimer les volumes
docker compose down

# Redémarrer avec les données
docker compose up -d
```

---

## Incidents fréquents

| Symptôme | Cause probable | Diagnostic | Action |
|----------|---|---|---|
| **migrator "Exited (1)"** | Migration en échec ou mot de passe incorrect | `docker compose logs migrator` | Vérifier les logs, corriger le schéma, relancer |
| **API ne démarre pas** | Migrator non terminé avec succès ou DB non accessible | `docker compose ps` et `docker compose logs api` | Attendre le migrator, vérifier la DB |
| **413 Request Entity Too Large** | `client_max_body_size` dans Nginx trop petit | Vérifier `nginx.conf` | Augmenter la limite dans `nginx.conf` |
| **404 Not Found au refresh (F5)** | `try_files` manquant dans Nginx | Vérifier `nginx.conf` | Ajouter `try_files $uri /index.html =404;` |
| **502 Bad Gateway** | Conteneur API arrêté ou en démarrage | `docker compose ps` et `curl http://localhost:8080/health/live` | Vérifier l'état de l'API, relancer si nécessaire |
| **Connection refused sur DB** | Conteneur DB non prêt ou port non exposé | `docker compose ps` et `docker compose logs db` | Attendre que DB démarre, vérifier les ports |
| **Erreur "database does not exist"** | Base de données non créée | `docker compose exec db psql -U postgres -l` | Exécuter le script d'initialisation |
| **Out of disk space** | Trop de logs ou de données accumulées | `docker system df` | Nettoyer les anciens logs, supprimer les volumes inutilisés |
| **Port déjà utilisé** | Port 8080 ou 5432 déjà occupé | `netstat -ano` (Windows) ou `lsof -i :8080` (Linux) | Arrêter le processus conflictuel ou modifier le port dans `docker-compose.yml` |
| **Mot de passe PostgreSQL rejeté** | Fichier `.env` non chargé ou valeur incorrecte | Vérifier `docker compose config` | Corriger `.env` et relancer `docker compose up` |

---

## Commandes utiles

### Nettoyer les ressources Docker
```powershell
# Supprimer les conteneurs arrêtés
docker container prune -f

# Supprimer les images inutilisées
docker image prune -f

# Supprimer les volumes inutilisés
docker volume prune -f

# Supprimer tout ce qui n'est pas utilisé (attention!)
docker system prune -a
```

### Accéder au shell d'un conteneur
```powershell
# Shell du conteneur API (.NET)
docker compose exec api bash
docker compose exec api powershell

# Shell du conteneur DB (PostgreSQL)
docker compose exec db bash
docker compose exec db psql -U postgres -d songvault
```

### Redémarrer un conteneur spécifique
```powershell
docker compose restart api
docker compose restart db
```

### Voir les détails d'un conteneur
```powershell
docker inspect songvault-api
docker inspect songvault-db
```

### Vérifier l'utilisation des ressources (CPU, mémoire)
```powershell
docker stats

# Ou spécifique à un conteneur
docker stats songvault-api
```

### Exporter / importer une image Docker
```powershell
# Exporter une image
docker save songvault-api:latest -o songvault-api.tar

# Importer une image
docker load -i songvault-api.tar
```

---

## Contacts et escalade

- **Problèmes persistants** : Consulter les logs complets avec `docker compose logs`
- **Performance dégradée** : Vérifier `docker stats` et les logs applicatifs
- **Données corrompues** : Restaurer depuis un backup précédent
- **Erreurs critiques** : Repartir de zéro après sauvegarde des données

---

## Voir aussi

- [docker-compose.yml](../docker-compose.yml) — Configuration complète
- [Dockerfile](../src/SongVault.Api/Dockerfile) — Image API
- [scripts/backup-files.ps1](../scripts/backup-files.ps1) — Sauvegarde automatisée
- [Documentation Docker Compose](https://docs.docker.com/compose/)
- [Documentation PostgreSQL](https://www.postgresql.org/docs/)
