<script setup lang="ts">
import { ref } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'
import { useBandStore } from '../stores/bandStore'
import { ApiError, getErrorMessage } from '@/shared/api/ApiError'
import type { BandRole } from '../types'
import { labels, t } from '@/shared/i18n'

const store = useBandStore()
const { bands, activeId } = storeToRefs(store)
const router = useRouter()

const roleLabels = labels<BandRole>({ Owner: 'bands.switcher.roles.Owner', Member: 'bands.switcher.roles.Member', Guest: 'bands.switcher.roles.Guest' })

const isCreating = ref(false)
const newName = ref('')
const error = ref<string | null>(null)
  /** #16 : refus de la formule gratuite (un seul groupe) ; lien vers les formules. */
const isPlanLimit = ref(false)

function onSelect(event: Event) {
  const bandId = (event.target as HTMLSelectElement).value
  void router.push({ name: 'songs', params: { bandId } })
}

async function onCreate() {
  error.value = null
  isPlanLimit.value = false
  try {
    const band = await store.create(newName.value)
    isCreating.value = false
    newName.value = ''
    await router.push({ name: 'songs', params: { bandId: band.id } })
  } catch (e) {
    error.value = getErrorMessage(e)
    isPlanLimit.value = e instanceof ApiError && e.status === 402
  }
}
</script>

<template>
  <div class="band-switcher">
    <label for="band-select">{{ t('bands.switcher.band') }}</label>
    <select id="band-select" :value="activeId ?? ''" @change="onSelect">
      <option v-for="band in bands" :key="band.id" :value="band.id">
        {{ band.name }} ({{ roleLabels[band.role] }})
      </option>
    </select>

    <button v-if="!isCreating" type="button" class="secondary" @click="isCreating = true">{{ t('bands.switcher.newBand') }}</button>
    <form v-else class="band-create" @submit.prevent="onCreate">
      <input v-model="newName" :aria-label="t('bands.switcher.newBandName')" :placeholder="t('bands.switcher.bandName')" required maxlength="100" />
      <button type="submit">{{ t('bands.switcher.create') }}</button>
      <button type="button" class="secondary" @click="isCreating = false">{{ t('common.actions.cancel') }}</button>
      <p v-if="error" role="alert" class="field-error">
        {{ error }}
        <RouterLink v-if="isPlanLimit" :to="{ name: 'plans' }">{{ t('bands.switcher.seePlans') }}</RouterLink>
      </p>
    </form>
  </div>
</template>

<style scoped>
.band-switcher { display: flex; align-items: center; gap: .5rem; flex-wrap: wrap; }
.band-create { display: flex; align-items: center; gap: .5rem; flex-wrap: wrap; }
</style>