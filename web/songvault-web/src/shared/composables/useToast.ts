import { readonly, ref } from 'vue'

export type ToastKind = 'success' | 'error' | 'info'

export interface Toast {
  id: number
  kind: ToastKind
  message: string
}

// État au niveau du module : partagé par toute l'application (un seul hôte d'affichage)
const toasts = ref<Toast[]>([])
let nextId = 1

export function useToast() {
  function dismiss(id: number) {
    toasts.value = toasts.value.filter((t) => t.id !== id)
  }

  function show(message: string, kind: ToastKind = 'success', durationMs = 4000) {
    const id = nextId++
    toasts.value.push({ id, kind, message })
    if (durationMs > 0) setTimeout(() => dismiss(id), durationMs)
    return id
  }

  return {
    toasts: readonly(toasts),
    show,
    dismiss,
    success: (message: string) => show(message, 'success'),
    error: (message: string) => show(message, 'error', 8000),   // une erreur reste plus longtemps
  }
}