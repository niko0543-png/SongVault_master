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