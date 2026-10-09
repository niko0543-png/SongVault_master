import { ref } from 'vue'

export interface ConfirmOptions {
  title: string
  message: string
  confirmLabel?: string
  danger?: boolean
}

interface PendingConfirm extends ConfirmOptions {
  resolve: (confirmed: boolean) => void
}

// Une seule question à la fois, pour toute l'application
const pending = ref<PendingConfirm | null>(null)

export function useConfirm() {
  /** Pose une question ; la Promise se résout à true (confirmé) ou false (annulé, Échap). */
  function confirm(options: ConfirmOptions): Promise<boolean> {
    pending.value?.resolve(false)                 // une question encore ouverte est annulée
    return new Promise<boolean>((resolve) => {
      pending.value = { ...options, resolve }
    })
  }

  function answer(confirmed: boolean) {
    const current = pending.value
    pending.value = null
    current?.resolve(confirmed)
  }

  return { pending, confirm, answer }
}