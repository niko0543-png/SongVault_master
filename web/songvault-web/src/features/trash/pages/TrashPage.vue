<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { trashApi } from '../api/trashApi'
import { daysLeft, kindLabel, trashLabel } from '../describe'
import type { TrashItem } from '../types'
import { useAuthStore } from '@/features/auth/stores/authStore'
import { useBandStore } from '@/features/bands/stores/bandStore'
import LoadingState from '@/shared/components/LoadingState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { getErrorMessage } from '@/shared/api/ApiError'
import { formatDate, formatFileSize } from '@/shared/utils/format'
import { useConfirm } from '@/shared/composables/useConfirm'
import { useToast } from '@/shared/composables/useToast'

const auth = useAuthStore()
const bands = useBandStore()
const { confirm } = useConfirm()
const toast = useToast()
const canPurge = computed(() => bands.can('admin'))

const items = ref<TrashItem[]>([])
const isLoading = ref(true)
const error = ref<unknown>(null)
const busyId = ref<string | null>(null)
const actionError = ref<string | null>(null)

async function load() {
  isLoading.value = true
  error.value = null
  try {
    items.value = await trashApi.list()
  } catch (e) {
    error.value = e
  } finally {
    isLoading.value = false
  }
}
watch(() => bands.activeId, (id) => { if (id) void load() }, { immediate: true })

function deletedBy(item: TrashItem): string {
  if (item.deletedById !== null && item.deletedById === auth.user?.id) return 'vous'
  return item.deletedByEmail ?? 'un compte supprimé'
}

function remaining(item: TrashItem): string {
  const days = daysLeft(item)
  return days <= 1 ? 'supprimé définitivement d’ici demain' : `encore ${days} jours`
}

async function run(item: TrashItem, action: () => Promise<void>, done: string) {
  busyId.value = item.id
  actionError.value = null
  try {
    await action()
    items.value = items.value.filter((i) => i.id !== item.id)
    toast.success(done)
    // Un morceau restauré ramène ses versions ; une version, ses fichiers : la liste se relit
    if (item.kind !== 'File') await load()
  } catch (e) {
    actionError.value = getErrorMessage(e)
  } finally {
    busyId.value = null
  }
}

function onRestore(item: TrashItem) {
  return run(item, () => trashApi.restore(item), `« ${trashLabel(item)} » est de retour.`)
}

async function onPurge(item: TrashItem) {
  const confirmed = await confirm({
    title: 'Supprimer définitivement ?',
    message: `« ${trashLabel(item)} » et tous ses fichiers seront effacés tout de suite. On ne pourra plus les restaurer.`,
    confirmLabel: 'Supprimer définitivement',
    danger: true,
  })
  if (!confirmed) return
  await run(item, () => trashApi.remove(item), `« ${trashLabel(item)} » est supprimé définitivement.`)
}
</script>

<template>
  <section>
    <header class="page-header">
      <h1>Corbeille</h1>
    </header>
    <p class="muted">
      Morceaux, versions et fichiers supprimés : ils se restaurent pendant 30 jours, puis partent pour de bon.
      Jusque-là, ils comptent dans le stockage du groupe.
    </p>

    <LoadingState v-if="isLoading" />
    <ErrorState v-else-if="error" :message="getErrorMessage(error)" @retry="load" />
    <EmptyState v-else-if="items.length === 0" message="La corbeille est vide." />

    <template v-else>
      <p v-if="actionError" class="field-error" role="alert">{{ actionError }}</p>
      <ul class="trash" aria-label="Éléments de la corbeille">
        <li v-for="item in items" :key="item.id" class="trash-item" data-testid="trash-item">
          <div class="what">
            <p>
              <span class="kind muted">{{ kindLabel(item) }}</span>
              <strong>{{ trashLabel(item) }}</strong>
              <span class="muted"> · {{ formatFileSize(item.sizeBytes) }}</span>
            </p>
            <p class="muted">
              <small>
                Supprimé par {{ deletedBy(item) }} le {{ formatDate(item.deletedAt) }} ·
                <time :datetime="item.purgeAt" :title="formatDate(item.purgeAt)">{{ remaining(item) }}</time>
              </small>
            </p>
          </div>
          <div class="actions">
            <button type="button" class="secondary" :disabled="busyId === item.id" @click="onRestore(item)">Restaurer</button>
            <button v-if="canPurge" type="button" class="danger" :disabled="busyId === item.id" @click="onPurge(item)">
              Supprimer définitivement
            </button>
          </div>
        </li>
      </ul>
    </template>
  </section>
</template>

<style scoped>
.trash { list-style: none; padding: 0; margin: 0; display: grid; gap: .5rem; }
.trash-item { display: flex; gap: 1rem; align-items: center; justify-content: space-between; flex-wrap: wrap;
  padding: .6rem .9rem; border: 1px solid var(--border); border-radius: var(--radius); background: var(--surface); }
.what { min-width: 0; flex: 1 1 18rem; }
.what p { margin: 0 0 .2rem; overflow-wrap: anywhere; }
.kind { font-size: .8rem; text-transform: uppercase; letter-spacing: .04em; margin-right: .5rem; }
.actions { display: flex; gap: .5rem; flex-wrap: wrap; }
</style>