# ADR 0049 — Pistes audio créées depuis le fichier Guitar Pro

- Statut : accepté
- Date : 2026-10-24

## Contexte
Une version qui a un fichier Guitar Pro n'a souvent pas encore d'enregistrement par instrument.
Le groupe veut pourtant répéter avec le mélangeur des pistes séparées (ADR 0030) : couper sa
propre partie, écouter la basse seule, jouer au clic. Le lecteur Guitar Pro (ADR 0034) sait déjà
jouer chaque piste du fichier avec le synthétiseur d'alphaTab.

Les sons RSE de Guitar Pro (échantillons réels, effets d'ampli) ne sont pas utilisables : leur
format n'est pas documenté et leur licence ne permet pas de les distribuer. Le son vient donc de
la banque General MIDI du lecteur (SoundFont).

## Décision
- **Rendu dans le navigateur**, par `api.exportAudio` d'alphaTab (depuis 1.6, sans nouveau paquet) :
  le morceau entier est synthétisé hors du temps réel, dans le Web Worker d'alphaTab, une fois par
  piste, avec le volume de toutes les autres pistes à zéro. Chaque fichier a la durée exacte du
  morceau : les pistes sont calées entre elles sans rien ajuster.
- **Banque de sons passée explicitement** (`soundFonts`), lue à `SOUND_FONT_URL`
  (`src/features/guitarpro/soundFont.ts`), la même que le lecteur : quand le lecteur suit une
  maquette (ADR 0036), il n'a pas de synthétiseur à prêter. Changer de banque de sons se fait à
  cet endroit seulement.
- **Piste « Clic »** facultative : le métronome seul, pour répéter au clic.
- **WAV PCM 16 bits stéréo à 44,1 kHz**, écrit à la main (`export/wav.ts`), converti morceau par
  morceau pendant le rendu. Sans perte, lu partout ; le serveur en tire l'aperçu MP3 du mélangeur
  (ADR 0021) comme pour tout WAV envoyé. Environ 10 Mo par minute et par piste : la taille totale
  est annoncée avant de lancer, puisqu'elle compte dans le quota du groupe (ADR 0023).
- **Fichiers ordinaires de la version**, envoyés par le parcours direct (ADR 0012), nommés
  « {tablature} - {piste}.wav », puis **ajoutés aux pistes séparées** après celles qui existent,
  dans la limite de 8. Une piste rendue sans un son (piste vide, instrument absent de la banque)
  n'est pas envoyée, et c'est dit. Après une erreur ou une annulation, les fichiers déjà envoyés
  deviennent quand même des pistes.
- Le tempo est celui du fichier (`useSyncPoints: false`), sans la vitesse, la transposition ni le
  mix choisis dans le lecteur : ce sont des réglages d'écoute.
- Bouton « Créer les pistes audio » sur la page du lecteur (ADR 0038), pour Member et Owner, pas
  dans la comparaison.

## Conséquences
- Front seulement : pas de changement d'API, pas de migration. Le serveur ne fait pas de rendu.
- Le rendu prend quelques secondes par piste et occupe l'appareil ; il s'annule entre deux étapes.
- Deux pistes du fichier qui partagent un canal MIDI (rare, sauf plusieurs pistes de batterie) se
  retrouvent ensemble dans chacun de leurs fichiers.
- Test Playwright `e2e/guitar-pro-export.spec.ts` : rendu réel dans Chromium, WAV relu par le
  navigateur, chaque fichier n'a que sa piste.
