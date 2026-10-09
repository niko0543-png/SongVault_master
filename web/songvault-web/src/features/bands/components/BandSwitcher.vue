<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'
import { useBandStore } from '../stores/bandStore'
import { getErrorMessage } from '@/shared/api/ApiError'
import type { BandRole } from '../types'

const store = useBandStore()
const { bands, activeId } = storeToRefs(store)
const router = useRouter()

const roleLabels: Record<BandRole, string> = { Owner: 'propriétaire', Member: 'membre', Guest: 'invité' }

const isCreating = ref(false)
const newName = ref('')
const error = ref<string | null>(null)

function onSelect(event: Event) {
  const bandId = (event.target as HTMLSelectElement).value
  void router.push({ name: 'songs', params: { bandId } })
}

async function onCreate() {
  error.value = null
  try {
    const band = await store.create(newName.value)
    isCreating.value = false
    newName.value = ''
    await router.push({ name: 'songs', params: { bandId: band.id } })
  } catch (e) {
    error.value = getErrorMessage(e)
  }
}
</script>

<template>
  <div class="band-switcher">
    <label for="band-select">Groupe</label>
    <select id="band-select" :value="activeId ?? ''" @change="onSelect">
      <option v-for="band in bands" :key="band.id" :value="band.id">
        {{ band.name }} ({{ roleLabels[band.role] }})
      </option>
    </select>

    <button v-if="!isCreating" type="button" class="secondary" @click="isCreating = true">Nouveau groupe</button>
    <form v-else class="band-create" @submit.prevent="onCreate">
      <input v-model="newName" aria-label="Nom du nouveau groupe" placeholder="Nom du groupe" required maxlength="100" />
      <button type="submit">Créer</button>
      <button type="button" class="secondary" @click="isCreating = false">Annuler</button>
      <p v-if="error" role="alert" class="field-error">{{ error }}</p>
    </form>
  </div>
</template>

<style scoped>
.band-switcher { display: flex; align-items: center; gap: .5rem; flex-wrap: wrap; }
.band-create { display: flex; align-items: center; gap: .5rem; flex-wrap: wrap; }
</style>