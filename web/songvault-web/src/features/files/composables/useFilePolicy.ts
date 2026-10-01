import { ref } from 'vue'
import { filesApi } from '../api/filesApi'
import type { FilePolicy } from '../types'

// Partagé par tous les composants : la politique n'est demandée qu'UNE fois
let cached: Promise<FilePolicy> | null = null

export function useFilePolicy() {
  const policy = ref<FilePolicy | null>(null)
  const error = ref<unknown>(null)

  cached ??= filesApi.policy().catch((e: unknown) => {
    cached = null              // en cas d'échec, on réessaiera au prochain appel
    throw e
  })
  cached.then(
    (p) => { policy.value = p },
    (e: unknown) => { error.value = e },
  )

  return { policy, error }
}
