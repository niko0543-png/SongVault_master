import type { StatusReport } from '../types'

export const statusApi = {
  /**
   * L'état du service, ou null si l'API ne répond pas (nginx renvoie alors 502 ou 504).
   * Pas de httpClient : un 503 porte aussi un état à afficher, et la page ne doit jamais déconnecter.
   */
  async get(): Promise<StatusReport | null> {
    try {
      const response = await fetch('/api/status', { headers: { Accept: 'application/json' }, cache: 'no-store' })
      if (response.status !== 200 && response.status !== 503) return null
      return (await response.json()) as StatusReport
    } catch {
      return null
    }
  },
}