import type { Widen } from './types'
import { commonMessages } from './common'
import { layoutMessages } from '@/app/locale'

/**
 * #26 : les catalogues de chaque partie de l'application, réunis. Pour en ajouter une : un fichier locale.ts
 * dans sa fonctionnalité (fr et en côte à côte), puis une ligne dans chacun des deux objets ci-dessous.
 */
export const fr = {
  common: commonMessages.fr,
  layout: layoutMessages.fr,
}

export const en: Widen<typeof fr> = {
  common: commonMessages.en,
  layout: layoutMessages.en,
}