import { ref, shallowRef } from 'vue'

/**
 * Enveloppe une action asynchrone : état de chargement, erreur,
 * et protection contre la double soumission.
 */
export function useAsyncAction<TArgs extends unknown[], TResult>(
  action: (...args: TArgs) => Promise<TResult>,
) {
  const isLoading = ref(false)
  const error = ref<unknown>(null)
  const lastResult = shallowRef<TResult>()

  async function run(...args: TArgs): Promise<TResult | undefined> {
    if (isLoading.value) return undefined          // double clic : ignoré
    isLoading.value = true
    error.value = null
    try {
      const result = await action(...args)
      lastResult.value = result
      return result
    } catch (e) {
      error.value = e
      return undefined
    } finally {
      isLoading.value = false
    }
  }

  return { isLoading, error, run, lastResult  }
}
