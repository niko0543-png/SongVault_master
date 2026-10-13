# ADR 0012 — Stockage objet Azure Blob, envoi direct par URL signée, hébergement Azure en France

- Statut : accepté
- Date : 2026-10-13

## Contexte
Un groupe qui travaille en studio échange des WAV de plusieurs centaines de mégaoctets. Avec
le stockage local de l'ADR 0002, chaque octet traverse nginx puis l'API, et la limite est de
50 Mo. Le volume Docker lie aussi les fichiers à une seule machine. Pour une version
commerciale, il faut un hébergement en UE, des fichiers qui ne dépendent pas d'un serveur,
et des envois de grande taille.

## Décision
- **`AzureBlobFileStorageService`** implémente `IFileStorageService`, à côté de
  `LocalFileStorageService`. `FileStorage:Provider` (`Local` ou `AzureBlob`) choisit au
  démarrage. Les fichiers sont dans un conteneur privé ; aucun accès anonyme.
- **Envoi direct** en trois temps, identique pour les deux stockages :
  1. `POST …/files/uploads` (nom, taille) : l'API vérifie l'extension, la taille (2 Go au
     plus), le groupe et le rôle, enregistre un `FileUpload` (valable 1 h) et renvoie
     l'adresse d'envoi. En `AzureBlob`, c'est une URL signée qui permet seulement de créer ce
     fichier (`sp=c`) ; en `Local`, c'est une route de l'API.
  2. `PUT` du fichier à cette adresse, par le navigateur.
  3. `POST …/files/uploads/{id}/complete` : l'API compare la taille réelle à la taille
     annoncée, contrôle la signature (32 premiers octets), crée le `SongFile` et efface le
     `FileUpload` dans la même transaction. En cas d'écart, le fichier est effacé (400).
- **Nettoyage** : une tâche de fond (ADR 0011) programmée à l'expiration efface un envoi
  jamais confirmé, ligne et fichier. `FileUploads` n'a pas de clé étrangère : la ligne
  survit à la suppression du morceau ou du compte, et la tâche efface alors le fichier.
- **Lecture** : `GET …/content` redirige (302) vers une URL signée de lecture, valable
  30 minutes, qui impose le type et le nom du fichier. En `Local`, l'API sert le fichier
  comme avant. Même chose pour l'archive d'export.
- L'envoi `POST …/files` (multipart, 50 Mo) reste pour les scripts et les tests.
- **Hébergement** : Azure, région France Central. Container Apps pour `web` (nginx) et
  `api` (au moins une instance, pour les tâches de fond), un job Container Apps pour
  `migrator`, Azure SQL Database au niveau Basic, un compte de stockage, Key Vault.
  L'infrastructure est dans `infra/main.bicep` ; `scripts/deploy-azure.ps1` déploie un tag
  d'image publié sur GitHub Container Registry.
- **Clés Data Protection** : toujours en base (ADR 0010), désormais chiffrées par une clé
  Key Vault quand `DataProtection:KeyVaultKeyUri` est défini. L'API y accède avec son
  identité managée.

## Alternatives écartées
- **Faire passer les gros fichiers par l'API** : 2 Go à travers nginx et Kestrel occupent
  une instance pendant tout l'envoi, et Container Apps limite la durée des requêtes.
- **URL signée seulement en `AzureBlob`, multipart en `Local`** : deux parcours dans le
  front et deux jeux de tests. La route `PUT` locale tient en quelques lignes.
- **Envoi par blocs avec le SDK JavaScript Azure** (`@azure/storage-blob`) : reprise après
  coupure et envois parallèles, mais une dépendance lourde et un parcours propre à Azure.
  Un `PUT` unique accepte jusqu'à 5 000 Mio, au-delà du besoin.
- **SAS par délégation d'utilisateur (identité managée, sans clé de compte)** : plus sûr,
  car aucune clé à protéger, mais impossible avec Azurite. C'est l'évolution à faire en
  premier si la clé devient un risque. La clé est aujourd'hui un secret de Container Apps.
- **Conteneur public avec des noms impossibles à deviner** : un lien qui fuit reste valable
  pour toujours.
- **Un VPS (OVH, Scaleway) avec Docker Compose** : moins cher, mais le stockage objet
  compatible S3 demanderait une autre implémentation, et la machine reste à administrer.
- **Azure App Service** : une seule application par plan à ce prix, et le job de migration
  serait à réinventer.

## Conséquences
- Précise, pour le cloud, la conséquence de l'ADR 0002 « deux éléments à sauvegarder
  ensemble » : la base et le compte de stockage ([ADR 0013](0013-sauvegardes-et-reprise.md)).
- Une URL signée peut être transmise à un tiers pendant sa durée de validité : 30 minutes
  pour lire, 1 heure pour créer un seul fichier. Les révoquer avant leur expiration demande
  de changer la clé du compte de stockage.
- Une SAS ne limite pas la taille : un envoi plus gros que prévu occupe le stockage jusqu'à
  la confirmation ou l'expiration. Les quotas par groupe arrivent en #16.
- Le front doit autoriser l'origine du stockage dans sa CSP (`connect-src`, `media-src`) :
  nginx la reçoit par la variable `STORAGE_ORIGIN`.
- Le stockage doit autoriser l'origine du site en CORS (`PUT`, `GET`) : Bicep le règle
  sur Azure, l'API le règle sur Azurite au démarrage (`FileStorage:AzureBlob:CorsOrigin`).
- `scripts/purge-orphan-files.ps1` et les sauvegardes de fichiers de `scripts/` ne
  concernent que le stockage `Local`.
- Coût de départ, pour un usage faible : de l'ordre de 10 à 20 € par mois, surtout
  l'instance d'API toujours active et la base Basic. Une base « sans serveur » ne se
  mettrait jamais en pause, car le service des tâches de fond l'interroge toutes les
  5 secondes.