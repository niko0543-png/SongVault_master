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
import { t } from '@/shared/i18n'

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
  return item.deletedByEmail ?? t('trash.page.deletedAccount')
}

function remaining(item: TrashItem): string {
  const days = daysLeft(item)
  return days <= 1 ? t('trash.page.purgeTomorrow') : t('trash.page.daysLeft', { count: days })
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
  return run(item, () => trashApi.restore(item), t('trash.page.restored', { item: trashLabel(item) }))
}

async function onPurge(item: TrashItem) {
  const confirmed = await confirm({
    title: t('trash.page.purgeTitle'),
    message: t('trash.page.purgeMessage', { item: trashLabel(item) }),
    confirmLabel: t('trash.page.purge'),
    danger: true,
  })
  if (!confirmed) return
  await run(item, () => trashApi.remove(item), t('trash.page.purged', { item: trashLabel(item) }))
}
</script>

<template>
  <section>
    <header class="page-header">
      <h1>{{ t('trash.page.title') }}</h1>
    </header>
    <p class="muted">
      {{ t('trash.page.intro') }}
    </p>

    <LoadingState v-if="isLoading" />
    <ErrorState v-else-if="error" :message="getErrorMessage(error)" @retry="load" />
    <EmptyState v-else-if="items.length === 0" :message="t('trash.page.empty')" />

    <template v-else>
      <p v-if="actionError" class="field-error" role="alert">{{ actionError }}</p>
      <ul class="trash" :aria-label="t('trash.page.listLabel')">
        <li v-for="item in items" :key="item.id" class="trash-item" data-testid="trash-item">
          <div class="what">
            <p>
              <span class="kind muted">{{ kindLabel(item) }}</span>
              <strong>{{ trashLabel(item) }}</strong>
              <span class="muted"> · {{ formatFileSize(item.sizeBytes) }}</span>
            </p>
            <p class="muted">
              <small>
                {{ t('trash.page.deletedBy', { item: deletedBy(item), deletedAt: formatDate(item.deletedAt) }) }}
                <time :datetime="item.purgeAt" :title="formatDate(item.purgeAt)">{{ remaining(item) }}</time>
              </small>
            </p>
          </div>
          <div class="actions">
            <button type="button" class="secondary" :disabled="busyId === item.id" @click="onRestore(item)">{{ t('trash.page.restore') }}</button>
            <button v-if="canPurge" type="button" class="danger" :disabled="busyId === item.id" @click="onPurge(item)">
              {{ t('trash.page.purge') }}
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