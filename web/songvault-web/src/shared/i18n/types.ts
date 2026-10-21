/**
 * #26 : forme des catalogues de traduction (ADR 0033). Le catalogue français fait foi : l'anglais doit avoir
 * exactement les mêmes clés, sinon vue-tsc échoue.
 */

/** Un texte qui dépend d'un nombre : `one` pour 1 (et 0 en français), `other` sinon ; `zero` facultatif. */
export interface Plural {
  zero?: string
  one: string
  other: string
}

/** Le catalogue d'arrivée : les mêmes clés que celui de départ, chaque texte étant une chaîne quelconque. */
export type Widen<T> = {
  [K in keyof T]: T[K] extends string ? string : T[K] extends Plural ? Plural : Widen<T[K]>
}

/** Toutes les clés d'un catalogue, sous la forme « songs.list.title ». */
export type Leaves<T, Prefix extends string = ''> = {
  [K in keyof T & string]: T[K] extends string | Plural ? `${Prefix}${K}` : Leaves<T[K], `${Prefix}${K}.`>
}[keyof T & string]

/** Les valeurs à placer dans un texte : « {name} » est remplacé par params.name ; count choisit le pluriel. */
export type Params = Record<string, string | number | null | undefined>