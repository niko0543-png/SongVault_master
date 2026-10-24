/**
 * Banque de sons du lecteur Guitar Pro et des pistes audio qu'il crée (ADR 0049).
 * Copiée à la racine du site par le plugin Vite d'alphaTab ; la changer ici suffit pour tout le lecteur.
 */
export const SOUND_FONT_URL = '/soundfont/sonivox.sf2'

/**
 * Gain appliqué au son de cette banque, pour qu'elle sonne aussi fort qu'une autre : 1 pour Sonivox.
 * Le rendu des pistes audio (export/renderStem.ts) l'applique tel quel.
 */
export const SOUND_FONT_GAIN = 1
