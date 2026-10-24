<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useBandStore } from '../stores/bandStore'
import { bandsApi } from '../api/bandsApi'
import { getErrorMessage } from '@/shared/api/ApiError'
import { useToast } from '@/shared/composables/useToast'
import { t } from '@/shared/i18n'

/** ADR 0048 : zone de suppression du groupe, sous la liste des membres. Affichée aux seuls Owner. */
const props = defineProps<{ memberCount: number }>()

const bands = useBandStore()
const router = useRouter()
const toast = useToast()

const typed = ref('')
const isBusy = ref(false)
const error = ref<string | null>(null)
const band = computed(() => bands.active)
// Même règle que l'API : le nom exact, espaces autour ignorés
const matches = computed(() => band.value !== null && typed.value.trim() === band.value.name)
const others = computed(() => Math.max(props.memberCount - 1, 0))

// Autre groupe ouvert sans quitter la page : on repart de zéro
watch(() => bands.activeId, () => { typed.value = ''; error.value = null })

async function onDelete() {
  const current = band.value
  if (!current || !matches.value) return
  isBusy.value = true
  error.value = null
  try {
    await bandsApi.remove(current.id, typed.value.trim())
    bands.remove(current.id)
    toast.success(t('bands.delete.deleted', { name: current.name }))
    await router.push({ name: 'home' })          // sans plus aucun groupe, « / » mène à la page Bienvenue
  } catch (e) {
    error.value = getErrorMessage(e)
  } finally {
    isBusy.value = false
  }
}
</script>

<template>
  <section v-if="band" class="card danger-zone" data-testid="delete-band">
    <h2>{{ t('bands.delete.title') }}</h2>
    <p>{{ t('bands.delete.warning') }}</p>
    <p v-if="others > 0">{{ t('bands.delete.members', { count: others }) }}</p>
    <p class="muted">{{ t('bands.delete.subscription') }}</p>
    <form novalidate @submit.prevent="onDelete">
      <div class="field">
        <label for="delete-band-name">{{ t('bands.delete.label', { name: band.name }) }}</label>
        <input id="delete-band-name" v-model="typed" autocomplete="off" :disabled="isBusy">
      </div>
      <p v-if="error" class="field-error" role="alert">{{ error }}</p>
      <button type="submit" class="danger" :disabled="!matches || isBusy">{{ t('bands.delete.button') }}</button>
    </form>
  </section>
</template>

<style scoped>
.danger-zone { margin-top: 2rem; border-color: var(--danger); }
.danger-zone h2 { margin-top: 0; color: var(--danger); }
</style>
