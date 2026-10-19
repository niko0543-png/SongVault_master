export interface CurrentUser {
  id: string
  email: string
}

export interface Credentials {
  email: string
  password: string
  /** #19 : code à 6 chiffres de l'application, demandé quand la double authentification est active */
  twoFactorCode?: string
  /** #19 : ou l'un des codes de secours, à la place */
  twoFactorRecoveryCode?: string
}