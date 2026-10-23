# ADR 0043 — Comparaison Guitar Pro : nouveau en vert, ancien en rouge, pistes en bas

- Statut : accepté
- Date : 2026-10-23

## Contexte
La comparaison ouverte par « Afficher les modifs » (ADR 0042) montrait seulement la partition
de B, avec les ajouts en vert et les modifications en orange ; ce qui avait disparu ou été
remplacé n'était visible que dans la liste. Le panneau des pistes du lecteur (ADR 0037) y était
masqué. Le musicien veut garder ce panneau et voir l'ancien en rouge, le nouveau en vert.

## Décision
- **Deux partitions, une à la fois** : un choix « Nouvelle version (B) » / « Ancienne version (A) »
  en haut de la liste des modifications. alphaTab dessine une partition par lecteur : on ne peut
  pas superposer les notes de A et de B dans la même portée.
- **Nouvelle version** : tout ce qui a été ajouté ou modifié est en **vert** (#1a7f37), à la place
  du vert et de l'orange de l'ADR 0035.
- **Ancienne version** : ce qui a été retiré ou remplacé est en **rouge** (#b42318). On l'obtient
  en calculant aussi les différences de B vers A et en les colorant dans la partition de A.
- La liste reste celle de A vers B ; un clic place la partition affichée sur la bonne mesure
  (celle de A pour l'ancienne : une mesure ajoutée dans B renvoie à la mesure de A qui suit).
- **Le panneau des pistes reste en bas** dans la comparaison, comme dans le lecteur habituel.

## Conséquences
- Front seulement : pas de changement d'API, pas de migration, pas de nouveau paquet npm.
- Deux calculs de différences par comparaison (dans des Web Workers) au lieu d'un ; le résumé
  de l'écran A/B n'en fait qu'un.
- Changer de partition recrée le lecteur : la lecture repart du début.
- Les numéros de mesure restent rouges, comme dans Guitar Pro (ADR 0037) : la légende et la liste
  disent le sens des couleurs, qui ne sont jamais seules.
