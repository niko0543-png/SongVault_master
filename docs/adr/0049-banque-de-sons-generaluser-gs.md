# ADR 0049 — Banque de sons GeneralUser GS pour le lecteur Guitar Pro

- Statut : accepté
- Date : 2026-10-24

## Contexte
Le lecteur Guitar Pro (ADR 0034) jouait avec Sonivox (1,3 Mo), la banque fournie avec alphaTab :
légère, mais les guitares saturées, la basse et la batterie sonnent comme un vieux MIDI. Les sons
RSE de Guitar Pro sont impossibles : ce moteur et ses échantillons sont propriétaires, le fichier
`.gp` ne contient que ses réglages, et alphaTab ne joue que des SoundFonts (`.sf2`, `.sf3`). Cette
banque servira aussi plus tard à produire des fichiers audio par piste à partir d'un Guitar Pro.

Comparées (même riff, guitare claire puis saturée, basse, batterie) :
- **GeneralUser GS 2.0.3** (S. Christian Collins), 32 Mo `.sf2` : nettement plus réaliste.
- **FluidR3Mono_GM** (MIT), 14,6 Mo `.sf3` : un peu moins bon, et le `.sf3` est encore
  « expérimental » dans alphaTab (décodage Vorbis au chargement).
- **MuseScore General** (MIT), ~40 Mo `.sf3`, ~200 Mo une fois décodée : trop lourde en mémoire
  sur téléphone.
- Musyng Kite, Arachno, Timbres of Heaven : licence floue ou non commerciale.

## Décision
- **GeneralUser GS 2.0.3** remplace Sonivox : `web/songvault-web/public/sounds/generaluser-gs-2.0.3.sf2`,
  avec sa licence à côté (`generaluser-gs-LICENSE.txt`). Elle est versionnée dans le dépôt
  (32 Mo, `*.sf2 binary` dans `.gitattributes`) : l'auteur demande de ne pas pointer vers ses
  téléchargements, et la construction ne dépend d'aucun site extérieur.
- **Licence** : usage libre pour la musique, privée ou commerciale, et invitation explicite à
  l'intégrer dans un logiciel. Réserve : l'auteur ne garantit pas l'origine de 100 % des
  échantillons (aucune réclamation depuis 2000). Si un échantillon posait problème, FluidR3Mono
  (MIT) est la solution de repli, sans autre changement que l'adresse.
- **Version dans le nom** : nginx sert `/sounds/` avec un cache d'un an (`immutable`) ; une
  nouvelle version de la banque prendra un nouveau nom. Le service worker n'y touche pas.
- **Volume** : la banque sort environ 6 dB moins fort que Sonivox ; le synthétiseur reçoit le
  volume général × 1,6 (+4 dB, crête à -0,5 dB sur le riff d'essai, sans saturer). La maquette
  suivie (ADR 0036) garde le volume tel quel.
- Le greffon Vite d'alphaTab copie toujours Sonivox dans `/soundfont/` ; elle n'est plus
  demandée.

## Conséquences
- La première ouverture d'une tablature télécharge 32 Mo au lieu de 1,3 Mo (environ 15 s en
  20 Mbit/s, la barre « Chargement des sons » le montre), puis plus rien grâce au cache.
  alphaTab garde la banque décodée en mémoire (environ 60 Mo).
- Le dépôt grossit de 32 Mo une fois.
- CSP inchangée : la banque vient de `'self'`.
- Les tests Playwright du lecteur passent avec la nouvelle banque (13 sur 13, trois fois).
