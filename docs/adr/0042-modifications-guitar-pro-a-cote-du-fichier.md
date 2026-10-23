# ADR 0042 — Modifications Guitar Pro résumées à côté du fichier, détail dans le lecteur

- Statut : accepté
- Date : 2026-10-23

## Contexte
La comparaison Guitar Pro (ADR 0035) ajoutait sous les différences de l'écran A/B une grande
section : choix des fichiers, liste des mesures, puis la partition colorée. Elle était loin du
fichier concerné, et la partition y était à l'étroit alors que le lecteur a maintenant sa page
plein écran (ADR 0038). Le musicien veut voir d'un coup d'œil ce qui a changé dans le fichier,
puis ouvrir la vue Guitar Pro avec les différences.

## Décision
- **Résumé à côté du fichier** : dans la liste « Fichiers » des différences (`VersionDiff`),
  chaque fichier Guitar Pro de B (ajouté ou gardé) porte un résumé : les changements
  d'ensemble (tempo, pistes, accordage, capodastre) puis une ligne par piste modifiée
  (« Guitare : 3 mesures modifiées, 1 mesure ajoutée »). `VersionDiff` reçoit pour cela un
  emplacement `file` ; l'écran A/B y place `GuitarProChangeSummary`, chargé à la demande.
- Le fichier de A comparé reste celui de l'ADR 0035 : le même nom sans la casse, sinon le premier
  fichier Guitar Pro de A. Sans fichier Guitar Pro dans A, rien ne s'affiche.
- **« Afficher les modifs »** ouvre la page de la tablature de B avec `?a={version A}&aFile={fichier A}`.
  Elle montre alors la partition colorée en plein cadre, la liste des modifications à gauche
  (au-dessus et repliable sous 900 px), un lien « Retour à la comparaison » qui garde les
  versions choisies, et « Sans les modifs » pour revenir au lecteur habituel.
- Le calcul est partagé (`useScoreCompare`) : lecture des deux fichiers, Web Worker, couleurs.
- Les listes de choix de fichiers A et B disparaissent : chaque fichier de B a son résumé.

## Conséquences
- Front seulement : pas de changement d'API, pas de migration, pas de nouveau paquet npm.
- L'écran A/B lit les deux fichiers pour le résumé, et la page du lecteur les relit : deux
  téléchargements, servis par le cache du navigateur quand le stockage le permet.
- En mode comparaison, le lecteur n'a ni calage sur la maquette (#29) ni réglages gardés par
  fichier, comme avant (la partition n'est pas celle du fichier seul).
- L'adresse de la page peut être partagée : elle ouvre les mêmes différences.
