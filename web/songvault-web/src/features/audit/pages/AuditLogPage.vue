<script setup lang="ts">
import { nextTick, ref, watch } from 'vue'
import { auditApi } from '../api/auditApi'
import { describe } from '../describe'
import type { AuditItem } from '../types'
import { useAuthStore } from '@/features/auth/stores/authStore'
import { useBandStore } from '@/features/bands/stores/bandStore'
import LoadingState from '@/shared/components/LoadingState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import EmptyState from '@/shared/components/EmptyState.vue'
import { getErrorMessage } from '@/shared/api/ApiError'
import { formatDate } from '@/shared/utils/format'
import { t } from '@/shared/i18n'

const auth = useAuthStore()
const bands = useBandStore()

const items = ref<AuditItem[]>([])
const nextCursor = ref<string | null>(null)
const isLoading = ref(true)
const error = ref<unknown>(null)
const isLoadingMore = ref(false)
const moreError = ref<string | null>(null)
const listEl = ref<HTMLOListElement | null>(null)

async function load() {
  isLoading.value = true
  error.value = null
  try {
    const page = await auditApi.list()
    items.value = page.items
    nextCursor.value = page.nextCursor
  } catch (e) {
    error.value = e
  } finally {
    isLoading.value = false
  }
}
watch(() => bands.activeId, (id) => { if (id) void load() }, { immediate: true })

/** Page suivante, ajoutée sous la liste ; le focus passe sur la première ligne arrivée. */
async function loadMore() {
  if (!nextCursor.value) return
  isLoadingMore.value = true
  moreError.value = null
  const firstNew = items.value.length
  try {
    const page = await auditApi.list(nextCursor.value)
    items.value = [...items.value, ...page.items]
    nextCursor.value = page.nextCursor
    await nextTick()
    listEl.value?.querySelectorAll<HTMLElement>('li')[firstNew]?.focus()
  } catch (e) {
    moreError.value = getErrorMessage(e)
  } finally {
    isLoadingMore.value = false
  }
}
</script>

<template>
  <section>
    <header class="page-header">
      <h1>{{ t('audit.log.title') }}</h1>
    </header>
    <p class="muted">
      {{ t('audit.log.intro') }}
    </p>

    <LoadingState v-if="isLoading" />
    <ErrorState v-else-if="error" :message="getErrorMessage(error)" @retry="load" />
    <EmptyState v-else-if="items.length === 0" :message="t('audit.log.empty')" />

    <template v-else>
      <ol ref="listEl" class="feed" :aria-label="t('audit.log.listLabel')">
        <li v-for="item in items" :key="item.id" class="feed-item" tabindex="-1" data-testid="audit-item">
          <p>
            {{ describe(item, auth.user?.id ?? null) }}
            <span v-if="item.details" class="muted">({{ item.details }})</span>
          </p>
          <time class="muted" :datetime="item.occurredAt"><small>{{ formatDate(item.occurredAt) }}</small></time>
        </li>
      </ol>

      <p v-if="moreError" class="field-error" role="alert">{{ moreError }}</p>
      <button v-if="nextCursor" type="button" class="secondary more" :disabled="isLoadingMore" @click="loadMore">
        {{ isLoadingMore ? t('audit.log.loading') : t('audit.log.more') }}
      </button>
      <p v-else class="muted"><small>{{ t('audit.log.end') }}</small></p>
    </template>
  </section>
</template>

<style scoped>
.feed { list-style: none; padding: 0; margin: 0; display: grid; gap: .5rem; }
.feed-item { padding: .6rem .9rem; border: 1px solid var(--border); border-radius: var(--radius); background: var(--surface); }
.feed-item p { margin: 0 0 .2rem; overflow-wrap: anywhere; }
.more { margin-top: 1rem; }
</style>