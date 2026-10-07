import { onScopeDispose, ref, watch, type Ref } from 'vue'

/** Copie de `source` mise à jour seulement après `delay` ms sans changement. */
export function useDebouncedRef<T>(source: Ref<T>, delay = 300): Ref<T> {
  const debounced = ref(source.value) as Ref<T>
  let timer: ReturnType<typeof setTimeout> | undefined

  watch(source, (value) => {
    clearTimeout(timer)
    timer = setTimeout(() => { debounced.value = value }, delay)
  })
  onScopeDispose(() => clearTimeout(timer))      // pas de minuteur orphelin quand le composant disparaît

  return debounced
}