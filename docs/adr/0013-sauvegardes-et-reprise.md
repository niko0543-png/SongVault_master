# ADR 0013 — Sauvegardes et reprise : services natifs d'Azure, verrous et exercice trimestriel

- Statut : accepté
- Date : 2026-10-14

## Contexte
Depuis l'ADR 0012, SongVault tourne sur Azure avec deux éléments à protéger ensemble : la base
Azure SQL et le compte de stockage des fichiers. Les scripts de `scripts/` ne sauvegardent que
l'installation Docker. Il faut des sauvegardes automatiques, une durée de conservation connue
(elle figure dans la politique de confidentialité), et la preuve qu'une restauration complète
marche et prend un temps connu.

## Décision
- **Objectifs** : RPO 1 heure, RTO 4 heures, écrits dans `docs/runbook-sauvegardes.md` avec
  ce que l'infrastructure permet réellement.
- **Base** : les sauvegardes automatiques d'Azure SQL, avec restauration à un instant donné
  sur **7 jours**, le maximum du niveau Basic. Pas de rétention longue.
- **Fichiers** : suppression réversible des fichiers et du conteneur pendant **14 jours**.
  Pas de versions de blobs : un fichier n'est jamais réécrit, sa clé est neuve à chaque envoi
  (ADR 0012).
- **Suppression par erreur** : un verrou `CanNotDelete` sur la base et sur le compte de
  stockage, posé par `infra/main.bicep`. Il bloque aussi la suppression du groupe de
  ressources entier.
- **Restauration en production** : la base est restaurée sous un autre nom, puis échangée par
  renommage ; `scripts/undelete-files-azure.ps1` récupère les fichiers supprimés depuis le même
  instant.
- **Exercice trimestriel** : `scripts/restore-drill-azure.ps1` recrée SongVault dans un groupe
  de ressources vierge à partir de la base restaurée et d'une copie des fichiers, et
  chronomètre chaque étape. Le workflow `restore-drill` ouvre une issue de rappel chaque
  trimestre. Les durées mesurées sont consignées dans le runbook.

## Alternatives écartées
- **Rétention longue hebdomadaire de la base** : protège si le serveur SQL lui-même est
  supprimé, mais garde les données supprimées des utilisateurs des semaines de plus. Le verrou
  couvre ce risque à moindre coût. À reprendre si un client exige un historique long.
- **Versions de blobs et restauration du stockage à un instant donné** : utiles quand des
  fichiers sont réécrits, ce qui n'arrive pas ici. Une règle de cycle de vie compte l'âge
  d'une version depuis l'écriture du fichier, pas depuis sa suppression : elle effacerait tout
  de suite la version d'un vieux fichier supprimé.
- **Copie vers un second compte de stockage** (réplication d'objets, ou Azure Backup
  « en coffre ») : protège d'une compromission du compte, mais double le stockage et ajoute un
  service à surveiller. À reprendre avec la supervision (#18).
- **Sauvegardes géoredondantes et reprise dans une autre région** : une panne complète de
  France Central n'est pas couverte. Passer `requestedBackupStorageRedundancy` à `Geo`
  permettrait une géorestauration de la base, mais le stockage (LRS) resterait dans la région.
- **Export régulier `.bacpac` vers le stockage** : redondant avec les sauvegardes d'Azure SQL,
  et un export de plus contenant des données personnelles.

## Conséquences
- Une donnée supprimée reste au plus 7 jours dans les sauvegardes de la base et 14 jours pour
  ses fichiers ; le registre RGPD et la politique de confidentialité l'indiquent.
- Après une restauration en production, les demandes de suppression de compte faites après
  l'instant choisi sont perdues et doivent être refaites (runbook) ; celles d'avant se
  relancent seules, leur tâche `account.purge` étant dans la base restaurée.
- Supprimer volontairement la base, le stockage ou le groupe de ressources demande d'abord de
  retirer les verrous.
- L'exercice coûte quelques centimes : l'environnement d'exercice vit quelques heures.