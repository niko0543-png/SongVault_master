export type DataExportStatus = 'Pending' | 'Ready' | 'Failed'

/** Archive des données du compte (GET /api/me/export). */
export interface DataExport {
  id: string
  status: DataExportStatus
  requestedAt: string
  completedAt: string | null
  expiresAt: string | null
  sizeBytes: number | null
}
/** #19 : double authentification (GET /api/me/two-factor). */
export interface TwoFactorStatus {
  enabled: boolean
  recoveryCodesLeft: number
  /** Ce navigateur ne redemande pas de code (14 jours après une connexion avec code). */
  browserRemembered: boolean
}

/** Clé à saisir dans l'application, par groupes de 4, et son lien otpauth:// (ouvert directement sur téléphone). */
export interface TwoFactorSetup {
  sharedKey: string
  authenticatorUri: string
}

/** Même liste que AccountEventType côté C# (#19). */
export type AccountEventType =
  | 'SignedIn' | 'SignedInWithCode' | 'SignedInWithRecoveryCode'
  | 'SignInFailed' | 'CodeRejected' | 'LockedOut'
  | 'PasswordChanged' | 'TwoFactorEnabled' | 'TwoFactorDisabled' | 'RecoveryCodesRegenerated'

/** Une ligne de l'historique de connexion (GET /api/me/account-events). */
export interface AccountEvent {
  id: number
  type: AccountEventType
  occurredAt: string
  /** Réseau d'origine, IP masquée (« 203.0.113.0/24 ») */
  network: string | null
  /** « Firefox sur Windows » */
  device: string | null
}