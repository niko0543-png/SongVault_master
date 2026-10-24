<script setup lang="ts">
import { ref } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { useBandStore } from '../stores/bandStore'
import { ApiError, getErrorMessage } from '@/shared/api/ApiError'
import { t } from '@/shared/i18n'

// Première connexion, ou plus aucun groupe après « Quitter le groupe » : l'utilisateur nomme son premier groupe
const bands = useBandStore()
const router = useRouter()

const name = ref('')
const isSaving = ref(false)
const error = ref<string | null>(null)
/** #16 : refus de la formule gratuite ; ne devrait pas arriver pour un premier groupe, mais l'API reste juge. */
const isPlanLimit = ref(false)

async function onCreate() {
  if (!name.value.trim() || isSaving.value) return
  error.value = null
  isPlanLimit.value = false
  isSaving.value = true
  try {
    const band = await bands.create(name.value.trim())
    await router.replace({ name: 'songs', params: { bandId: band.id } })
  } catch (e) {
    error.value = getErrorMessage(e)
    isPlanLimit.value = e instanceof ApiError && e.status === 402
  } finally {
    isSaving.value = false
  }
}
</script>

<template>
  <section class="welcome">
    <h1>{{ t('bands.welcome.title') }}</h1>
    <p>{{ t('bands.welcome.intro') }}</p>
    <form class="card" @submit.prevent="onCreate">
      <div class="field">
        <label for="welcome-band-name">{{ t('bands.switcher.bandName') }}</label>
        <input id="welcome-band-name" v-model="name" required maxlength="100" autocomplete="off"
               :placeholder="t('bands.welcome.placeholder')" />
      </div>
      <p v-if="error" role="alert" class="field-error">
        {{ error }}
        <RouterLink v-if="isPlanLimit" :to="{ name: 'plans' }">{{ t('bands.switcher.seePlans') }}</RouterLink>
      </p>
      <button type="submit" :disabled="isSaving || !name.trim()">
        {{ isSaving ? t('common.actions.saving') : t('bands.welcome.create') }}
      </button>
    </form>
    <p class="muted"><small>{{ t('bands.welcome.invited') }}</small></p>
  </section>
</template>

<style scoped>
.welcome { max-width: 32rem; margin: 2rem auto; }
</style>
