# ADR 0044 — Sortie d'un morceau : infos de sortie sur la dernière version Final

- Statut : accepté
- Date : 2026-10-23

## Contexte
Un morceau fini restait dans la liste des morceaux, au milieu de ceux en travail, avec toutes ses
versions et leurs fichiers, qui comptent dans le quota du groupe (ADR 0023). Les sorties (ADR 0016)
existaient, mais rien ne disait qu'un morceau était paru, ni où l'écouter. Le musicien veut ajouter
les infos de sortie sur la dernière version finale, voir alors le morceau passer dans l'onglet
Sorties, choisir s'il garde toutes les versions ou seulement la dernière pour libérer de la place,
et noter les liens d'écoute des plateformes.

## Décision
- **Sur la dernière version, au statut Final** : la page de cette version porte une section
  « Sortie ». Une autre version, ou une dernière version qui n'est pas Final, est refusée par
  l'API (422) ; le front explique comment faire.
- **La sortie porte les infos** : enregistrer crée un single au titre du morceau, ou fait
  rejoindre une sortie existante (EP, album) dans cette version, en dernière piste ou à sa place
  s'il y figure déjà. Date de sortie, label, code-barres UPC/EAN (clé vérifiée) et liens d'écoute
  vont sur la sortie : un album a un seul lien Spotify, et l'archive du distributeur (ADR 0031) a
  ainsi tout sous la main. Les mêmes infos se modifient sur la page de chaque sortie
  (`PUT …/releases/{id}/distribution`).
- **Le morceau sait qu'il est sorti** : `Songs.ReleaseId` (clé étrangère SET NULL) et `ReleasedAt`.
  La liste des morceaux demande `?released=false` ; la recherche des pistes et des setlists voit
  toujours tous les morceaux. « Remettre dans les morceaux » vide `ReleaseId` et laisse la
  sortie intacte ; retirer le morceau de sa sortie, ou supprimer la sortie, le remet aussi en travail.
- **Liens d'écoute** : une adresse http(s) par lien, douze au plus, sans doublon ; la plateforme est
  reconnue au nom de domaine (Spotify, Apple Music, Deezer, YouTube Music, YouTube, Bandcamp,
  SoundCloud, Tidal, Amazon Music, sinon Other). Colonne JSON sur `Releases`, comme les points de
  calage (ADR 0036) : les liens se lisent et s'écrivent ensemble.
- **La question des versions**, posée avant l'envoi quand le morceau en a plusieurs :
  « Garder toutes les versions » ou « Ne garder que la vN ». Les autres versions vont à la
  corbeille (ADR 0027) : restaurables 30 jours, comptées dans le quota jusqu'à la purge. Un
  propriétaire peut cocher « Libérer la place tout de suite » : elles sont alors effacées par
  `TrashPurger`, après la validation en base, et la jauge est recalculée (un membre reçoit 403).
- Une version qu'une autre sortie ou une setlist utilise encore reste ; la réponse dit laquelle et
  où, et la page l'affiche.
- Tout est vérifié avant la moindre écriture ; sortie, piste, morceau et mise à la corbeille sont
  enregistrés dans une même transaction. Le journal d'audit note `SongReleased` avec la version et
  ce qui est parti.

## Conséquences
- Migration `AddSongRelease` : `Songs.ReleaseId`, `Songs.ReleasedAt`, `Releases.Label`, `Releases.Upc`,
  `Releases.StreamingLinks` (les sorties existantes reçoivent `[]`). Les morceaux existants restent
  dans la liste des morceaux jusqu'à leur sortie.
- La purge d'un compte (ADR 0011) efface les sorties avant les morceaux : SET NULL ne gêne pas.
- Un morceau sorti reste ouvrable, modifiable et lisible par les liens de partage ; seule sa place
  dans les listes change. Une nouvelle version après la sortie ne le remet pas en travail.
- Pas de nouveau paquet NuGet ni npm.

## Révision (#39, 2026-10-23)
- **L'onglet Sorties ne se saisit plus** : plus de création de sortie, ni de modification du titre, du type,
  de la date ou des infos de sortie, ni d'ajout, de retrait ou de changement de version d'une piste. Une sortie
  et ses infos ne viennent que de la section « Sortie » de la dernière version finale d'un morceau.
- Cette section crée donc aussi un **nouvel EP ou un nouvel album** (titre saisi, celui du morceau par défaut),
  en plus du nouveau single et des sorties existantes (`newReleaseType`, `newReleaseTitle`).
- Une fois le morceau sorti, ses infos de sortie (label, UPC/EAN, liens d'écoute) **se corrigent sur la page de
  la version**. Une date à corriger : « Remettre dans les morceaux », puis ressortir sur la même sortie.
- Restent sur la page d'une sortie : la pochette, l'ordre des pistes, l'archive du distributeur, les liens de
  partage, l'export des crédits et la suppression.
- L'API garde ses routes (`POST/PUT …/releases`, `PUT …/tracks`, `PUT …/distribution`) : seul le front cesse de
  les proposer ailleurs que depuis la version.
