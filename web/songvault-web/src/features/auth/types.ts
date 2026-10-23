export interface CurrentUser {
  id: string
  email: string
  /** #26 : langue enregistrée sur le compte ; null tant que l'utilisateur n'en a pas choisi */
  locale?: string | null
}

export interface Credentials {
  email: string
  password: string
  /** #19 : code à 6 chiffres de l'application, demandé quand la double authentification est active */
  twoFactorCode?: string
  /** #19 : ou l'un des codes de secours, à la place */
  twoFactorRecoveryCode?: string
}