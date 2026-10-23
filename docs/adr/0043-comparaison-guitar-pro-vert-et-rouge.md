# ADR 0043 — Comparaison Guitar Pro : ajouté en vert, modifié en orange, retiré en rouge, pistes en bas

- Statut : accepté
- Date : 2026-10-23

## Contexte
La comparaison ouverte par « Afficher les modifs » (ADR 0042) montrait seulement la partition
de B, avec les ajouts en vert et les modifications en orange ; ce qui avait disparu ou été
remplacé n'était visible que dans la liste. Le panneau des pistes du lecteur (ADR 0037) y était
masqué. Le musicien veut garder ce panneau, voir l'ajouté en vert, le modifié en orange et le
retiré en rouge.

## Décision
- **Deux partitions, une à la fois** : un choix « Nouvelle version (B) » / « Ancienne version (A) »
  en haut de la liste des modifications. alphaTab dessine une partition par lecteur : on ne peut
  pas superposer les notes de A et de B dans la même portée.
- **Nouvelle version** : ce qui a été ajouté en **vert** (#1a7f37), ce qui a été modifié en
  **orange** (#b35900), comme dans l'ADR 0035.
- **Ancienne version** : ce qui a été retiré en **rouge** (#b42318), les notes remplacées en
  **orange**. On l'obtient en calculant aussi les différences de B vers A et en les colorant dans
  la partition de A.
- La liste reste celle de A vers B ; un clic place la partition affichée sur la bonne mesure
  (celle de A pour l'ancienne : une mesure ajoutée dans B renvoie à la mesure de A qui suit).
- **Le panneau des pistes reste en bas** dans la comparaison, comme dans le lecteur habituel.
- **Changer de partition garde l'instrument et la mesure** : le lecteur dit ses pistes affichées et
  sa mesure (`position()`), la comparaison les retrouve dans l'autre version (pistes associées par
  l'ADR 0035, mesure par l'alignement des différences de la piste : `matchingBar`) et les redonne
  au nouveau lecteur (`startTracks`, `startBar`). Une mesure qui n'existe que d'un côté renvoie à
  la suivante de l'autre. La mesure retenue est celle du curseur (un clic sur une note l'y
  place) si on la voit, sinon la première mesure visible en haut du cadre.
- Le nouveau lecteur ne se place qu'une fois la partition dessinée et ses mesures placées
  (`postRenderFinished`) et le son prêt ; alphaTab refait alors défiler le cadre vers l'ancienne
  place de son curseur, on refait défiler après lui.

## Conséquences
- Front seulement : pas de changement d'API, pas de migration, pas de nouveau paquet npm.
- Deux calculs de différences par comparaison (dans des Web Workers) au lieu d'un ; le résumé
  de l'écran A/B n'en fait qu'un.
- Changer de partition recrée le lecteur : la lecture s'arrête, et repart de la mesure retrouvée.
- Les numéros de mesure restent rouges, comme dans Guitar Pro (ADR 0037) : la légende et la liste
  disent le sens des couleurs, qui ne sont jamais seules.
