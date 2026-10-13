<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import SectionCard from '@/shared/components/SectionCard.vue'
import { getErrorMessage } from '@/shared/api/ApiError'
import { useConfirm } from '@/shared/composables/useConfirm'
import { useToast } from '@/shared/composables/useToast'
import { useAuthStore } from '@/features/auth/stores/authStore'
import { accountApi } from '../api/accountApi'

const router = useRouter()
const auth = useAuthStore()
const toast = useToast()
const { confirm } = useConfirm()

const password = ref('')
const error = ref<string | null>(null)
const isDeleting = ref(false)

async function onSubmit() {
  error.value = null
  if (!password.value) {
    error.value = 'Saisissez votre mot de passe.'
    return
  }
  const confirmed = await confirm({
    title: 'Supprimer votre compte ?',
    message:
      'Vous quittez tout de suite vos groupes partagés. Dans 30 jours, le compte sera effacé définitivement, ' +
      'avec les groupes dont vous êtes le seul membre et leurs fichiers.',
    confirmLabel: 'Supprimer mon compte',
    danger: true,
  })
  if (!confirmed) return

  isDeleting.value = true
  try {
    await accountApi.deleteAccount(password.value)
    auth.clearSession()
    toast.success('Compte supprimé. Il sera effacé définitivement dans 30 jours.')
    await router.replace({ name: 'login' })
  } catch (e) {
    error.value = getErrorMessage(e)
  } finally {
    isDeleting.value = false
  }
}
</script>

<template>
  <SectionCard title="Supprimer mon compte">
    <p class="muted">
      Si vous êtes le dernier Owner d’un groupe qui a d’autres membres, nommez d’abord un autre Owner.
      Pour garder une copie de vos données, exportez-les avant.
    </p>
    <form class="delete-form" novalidate @submit.prevent="onSubmit">
      <div class="field">
        <label for="delete-password">Mot de passe</label>
        <input id="delete-password" v-model="password" type="password" autocomplete="current-password" required />
      </div>
      <p v-if="error" class="field-error" role="alert">{{ error }}</p>
      <button type="submit" class="danger" :disabled="isDeleting">
        {{ isDeleting ? 'Suppression…' : 'Supprimer mon compte' }}
      </button>
    </form>
  </SectionCard>
</template>

<style scoped>
.delete-form { max-width: 26rem; }
</style>