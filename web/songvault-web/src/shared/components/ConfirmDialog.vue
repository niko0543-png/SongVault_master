<script setup lang="ts">
import { nextTick, useTemplateRef, watch } from 'vue'
import { useConfirm } from '../composables/useConfirm'

const { pending, answer } = useConfirm()
const dialog = useTemplateRef<HTMLDialogElement>('confirm-dialog')

// Ouvre ou ferme la boîte native quand une question arrive ou se termine
watch(pending, async (value) => {
  await nextTick()                                  // laisse Vue afficher le contenu avant showModal
  const el = dialog.value
  if (!el) return
  if (value && !el.open) {
    el.returnValue = ''                             // sinon la réponse précédente serait relue
    el.showModal()
  } else if (!value && el.open) {
    el.close()
  }
})

// Déclenché par les boutons du formulaire ET par la touche Échap
function onClose() {
  if (pending.value) answer(dialog.value?.returnValue === 'confirm')
}
</script>

<template>
  <dialog
    ref="confirm-dialog"
    class="confirm-dialog"
    aria-labelledby="confirm-title"
    aria-describedby="confirm-message"
    @close="onClose"
  >
    <form v-if="pending" method="dialog">
      <h2 id="confirm-title">{{ pending.title }}</h2>
      <p id="confirm-message">{{ pending.message }}</p>
      <div class="actions">
        <button value="cancel" class="secondary" autofocus>Annuler</button>
        <button value="confirm" :class="{ danger: pending.danger }">{{ pending.confirmLabel ?? 'Confirmer' }}</button>
      </div>
    </form>
  </dialog>
</template>

<style scoped>
.confirm-dialog { border: none; border-radius: var(--radius); padding: 1.5rem; max-width: min(28rem, calc(100vw - 2rem)); }
.confirm-dialog::backdrop { background: rgb(0 0 0 / .45); }
.confirm-dialog h2 { margin-top: 0; font-size: 1.2rem; }
.actions { display: flex; justify-content: flex-end; gap: .5rem; }
</style>