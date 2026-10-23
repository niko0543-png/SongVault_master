# ADR 0045 — Onglet « Morceaux » et réglages de volume

- Statut : accepté
- Date : 2026-10-23

## Contexte
La liste des morceaux d'un groupe n'avait pas d'entrée dans la navigation : on y revenait par le
logo. Le lecteur audio du bas jouait toujours au volume plein, sans bouton muet. Dans le lecteur
Guitar Pro, chaque piste avait son volume (ADR 0037), mais rien ne baissait tout le morceau d'un
coup, comme la piste master de Guitar Pro.

## Décision
- **Onglet « Morceaux »** en tête de la navigation, avant « Sorties ». L'onglet de la page affichée
  est souligné ; la page d'un morceau, d'une version ou d'une tablature reste sous « Morceaux »,
  une sortie sous « Sorties », une setlist sous « Setlists ».
- **Volume du lecteur audio** : un bouton muet (🔊, 🔉, 🔇 selon le niveau) et une glissière de 0
  à 100 % dans la barre du bas. Monter le volume rétablit le son coupé ; couper garde le niveau
  choisi. Volume et muet sont gardés sur l'appareil (`songvault.player.volume`), pas dans le
  compte. Sur téléphone, seul le bouton muet reste : les boutons de l'appareil règlent le volume,
  et iOS ignore celui de la page.
- **Piste « Général » du lecteur Guitar Pro** : une ligne en bas du panneau des pistes, avec une
  glissière de 0 à 100 % (`api.masterVolume` d'alphaTab), sans toucher au mix des pistes. Elle
  reste active quand la tablature suit une maquette (ADR 0036) : elle règle alors le volume de la
  maquette. Le niveau est le même pour toutes les tablatures et gardé sur l'appareil, avec les
  autres réglages d'affichage (ADR 0039).
- Le volume général va de 0 à 100 % (et non jusqu'à 200 % comme une piste) : un élément `<audio>`
  n'accepte pas plus de 1.

## Conséquences
- Front seulement : pas de changement d'API, pas de migration, pas de nouveau paquet npm.
- Les volumes ne suivent pas le compte d'un appareil à l'autre ; c'est voulu (casque, enceintes).
- Le lecteur audio du bas et la tablature ont chacun leur volume : ils ne jouent jamais ensemble.
