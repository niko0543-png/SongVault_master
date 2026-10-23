<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { setlistsApi, toChoices } from '../api/setlistsApi'
import {
  SETLIST_COMMENT_MAX, SETLIST_MAX_ITEMS, SETLIST_NOTES_MAX,
  type Setlist, type SetlistInput, type SetlistItem, type SetlistItemChoice,
} from '../types'
import { formatDuration, formatSetlistDate, formatTotal, parseDuration } from '../format'
import SetlistForm from '../components/SetlistForm.vue'
import SetlistPrintSheet from '../components/SetlistPrintSheet.vue'
import SongPicker from '../components/SongPicker.vue'
import type { Song } from '@/features/songs/types'
import { useBandStore } from '@/features/bands/stores/bandStore'
import LoadingState from '@/shared/components/LoadingState.vue'
import ErrorState from '@/shared/components/ErrorState.vue'
import SectionCard from '@/shared/components/SectionCard.vue'
import ReorderableList from '@/shared/components/ReorderableList.vue'
import { ApiError, getErrorMessage } from '@/shared/api/ApiError'
import { useConfirm } from '@/shared/composables/useConfirm'
import { useToast } from '@/shared/composables/useToast'
import { t } from '@/shared/i18n'

const props = defineProps<{ setlistId: string }>()
const bands = useBandStore()
const router = useRouter()
const { confirm } = useConfirm()
const toast = useToast()
const canWrite = computed(() => bands.can('write'))

// ---- Chargement ----
const setlist = ref<Setlist | null>(null)
const isLoading = ref(true)
const error = ref<unknown>(null)
const isNotFound = computed(() => error.value instanceof ApiError && error.value.status === 404)

async function load(id: string) {
  isLoading.value = true
  error.value = null
  try {
    setlist.value = await setlistsApi.get(id)
  } catch (e) {
    error.value = e
  } finally {
    isLoading.value = false
  }
}
watch(() => props.setlistId, load, { immediate: true })

// ---- Nom, date, lieu ----
const isEditing = ref(false)
const detailsError = ref<string | null>(null)
const savingDetails = ref(false)

async function onSaveDetails(input: SetlistInput) {
  if (!setlist.value) return
  savingDetails.value = true
  detailsError.value = null
  try {
    await setlistsApi.update(setlist.value.id, input)
    setlist.value = { ...setlist.value, ...input }
    isEditing.value = false
  } catch (e) {
    detailsError.value = getErrorMessage(e)
  } finally {
    savingDetails.value = false
  }
}

async function onDelete() {
  if (!setlist.value) return
  const { id, name } = setlist.value
  const confirmed = await confirm({
    title: t('setlists.detail.deleteTitle'),
    message: t('setlists.detail.deleteMessage', { name }),
    confirmLabel: t('common.actions.delete'),
    danger: true,
  })
  if (!confirmed) return
  try {
    await setlistsApi.remove(id)
    toast.success(t('setlists.detail.deleted', { name }))
    await router.push({ name: 'setlists' })
  } catch (e) {
    detailsError.value = getErrorMessage(e)
  }
}

function onPrint() {
  window.print()
}

// ---- Lignes : chaque changement enregistre la liste complète, et l'API renvoie le nouveau total (ADR 0028) ----
const savingItems = ref(false)
const itemsError = ref<string | null>(null)

/** Enregistre ces lignes ; shown est ce que la page affiche pendant l'enregistrement. */
async function saveItems(choices: SetlistItemChoice[], shown?: SetlistItem[]) {
  const current = setlist.value
  if (!current) return
  savingItems.value = true
  itemsError.value = null
  if (shown) setlist.value = { ...current, items: shown.map((item, i) => ({ ...item, position: i + 1 })) }
  try {
    setlist.value = await setlistsApi.setItems(current.id, choices)
  } catch (e) {
    setlist.value = current                       // refusé : on revient à l'ordre enregistré
    itemsError.value = getErrorMessage(e)
  } finally {
    savingItems.value = false
  }
}

function onMove(items: SetlistItem[]) {
  void saveItems(toChoices({ items }), items)
}

/** Modifie une ligne (version, notes ou durée) et enregistre la liste complète. */
function saveLine(item: SetlistItem, change: Partial<SetlistItemChoice>) {
  if (!setlist.value) return
  void saveItems(toChoices(setlist.value).map((c) => (c.songId === item.songId ? { ...c, ...change } : c)))
}

function onChangeVersion(item: SetlistItem, event: Event) {
  saveLine(item, { versionId: (event.target as HTMLSelectElement).value || null })
}

function onChangeNotes(item: SetlistItem, event: Event) {
  saveLine(item, { notes: (event.target as HTMLInputElement).value.trim() || null })
}

function onChangeDuration(item: SetlistItem, event: Event) {
  const input = event.target as HTMLInputElement
  const durationMs = parseDuration(input.value)
  if (durationMs === undefined) {
    itemsError.value = t('setlists.detail.badDuration', { songTitle: item.songTitle })
    input.value = formatDuration(item.manualDurationMs)
    return
  }
  saveLine(item, { durationMs })
}

// ---- Commentaires entre les morceaux (ADR 0046) : celui d'une ligne se lit juste avant elle ----
/** Lignes dont on a ouvert le champ commentaire, encore vide */
const openComments = ref<string[]>([])

function showComment(item: SetlistItem): boolean {
  return !!item.commentBefore || openComments.value.includes(item.songId)
}

function onOpenComment(item: SetlistItem) {
  openComments.value = [...openComments.value, item.songId]
  void nextTick(() => document.getElementById(`comment-${item.songId}`)?.focus())
}

function onChangeComment(item: SetlistItem, event: Event) {
  const commentBefore = (event.target as HTMLTextAreaElement).value.trim() || null
  if (!commentBefore) openComments.value = openComments.value.filter((id) => id !== item.songId)
  if (commentBefore === item.commentBefore) return
  saveLine(item, { commentBefore })
}

function onRemoveItem(item: SetlistItem) {
  if (!setlist.value) return
  const rest = setlist.value.items.filter((i) => i.songId !== item.songId)
  void saveItems(toChoices({ items: rest }), rest)
}

function playedLabel(item: SetlistItem): string {
  const v = item.versions.find((x) => x.id === item.playedVersionId)
  if (!v) return t('setlists.detail.noVersion')
  return `${item.versionId ? '' : `${t('setlists.detail.latestPrefix')} `}v${v.number} · ${v.title}`
}

function musicLabel(item: SetlistItem): string {
  return [item.key, item.bpm ? `${item.bpm} BPM` : null].filter(Boolean).join(' · ')
}

// ---- Ajout de morceaux existants (ADR 0046) ----
const songIds = computed(() => setlist.value?.items.map((i) => i.songId) ?? [])
const remaining = computed(() => SETLIST_MAX_ITEMS - songIds.value.length)

/** Ajoute les morceaux cochés en fin de setlist, dans l'ordre où on les a cochés, joués dans leur dernière version. */
function onAdd(songs: Song[]) {
  if (!setlist.value) return
  const added = songs.filter((s) => !songIds.value.includes(s.id))
    .map((s) => ({ songId: s.id, versionId: null, notes: null, durationMs: null, commentBefore: null }))
  void saveItems([...toChoices(setlist.value), ...added])
}
</script>

<template>
  <section>
    <div class="print-hide">
      <RouterLink :to="{ name: 'setlists' }">{{ t('setlists.detail.back') }}</RouterLink>
    </div>

    <LoadingState v-if="isLoading" />
    <div v-else-if="isNotFound" class="card" role="alert"><p>{{ t('setlists.detail.notFound') }}</p></div>
    <ErrorState v-else-if="error" :message="getErrorMessage(error)" @retry="load(setlistId)" />

    <template v-else-if="setlist">
      <div class="print-hide">
        <header class="page-header">
          <div>
            <h1>{{ setlist.name }}</h1>
            <p class="muted">{{ formatSetlistDate(setlist.date) }}<template v-if="setlist.venue"> · {{ setlist.venue }}</template></p>
          </div>
          <div v-if="!isEditing" class="actions">
            <button type="button" class="secondary" :disabled="setlist.items.length === 0" @click="onPrint">{{ t('setlists.detail.print') }}</button>
            <template v-if="canWrite">
              <button type="button" class="secondary" @click="isEditing = true">{{ t('setlists.detail.edit') }}</button>
              <button type="button" class="danger" @click="onDelete">{{ t('common.actions.delete') }}</button>
            </template>
          </div>
        </header>
        <p v-if="detailsError && !isEditing" class="field-error" role="alert">{{ detailsError }}</p>
        <SectionCard v-if="isEditing" :title="t('setlists.detail.editTitle')">
          <SetlistForm :initial="{ name: setlist.name, date: setlist.date, venue: setlist.venue }"
                       :submit-label="t('common.actions.save')" :submitting="savingDetails" :error="detailsError"
                       @submit="onSaveDetails" @cancel="isEditing = false" />
        </SectionCard>

        <SectionCard :title="t('setlists.detail.songs', { count: setlist.items.length })">
          <p class="total" data-testid="setlist-total">
            {{ t('setlists.detail.total') }} <strong>{{ formatTotal(setlist.totalDurationMs) }}</strong> <span
              v-if="setlist.missingDurationCount > 0" class="muted">
              {{ t('setlists.missingDuration', { count: setlist.missingDurationCount }) }}
            </span>
          </p>
          <p v-if="itemsError" class="field-error" role="alert">{{ itemsError }}</p>
          <p v-if="setlist.items.length === 0" class="muted">{{ t('setlists.detail.empty') }}</p>
          <ReorderableList v-else :items="setlist.items" :item-key="(i) => i.songId" :item-label="(i) => i.songTitle"
                           :label="t('setlists.detail.itemsLabel')" :disabled="!canWrite || savingItems" @move="onMove">
            <template #default="{ item }">
              <div v-if="showComment(item)" class="between" data-testid="setlist-comment">
                <textarea v-if="canWrite" :id="`comment-${item.songId}`" :value="item.commentBefore ?? ''" rows="1"
                          :maxlength="SETLIST_COMMENT_MAX" :placeholder="t('setlists.detail.commentPlaceholder')" :disabled="savingItems"
                          :aria-label="t('setlists.detail.commentBefore', { songTitle: item.songTitle })"
                          @change="onChangeComment(item, $event)" />
                <p v-else>{{ item.commentBefore }}</p>
              </div>
              <button v-else-if="canWrite" type="button" class="add-comment" :disabled="savingItems"
                      :aria-label="t('setlists.detail.addCommentBefore', { songTitle: item.songTitle })"
                      @click="onOpenComment(item)">{{ t('setlists.detail.addComment') }}</button>
              <div class="line" data-testid="setlist-line">
                <span class="number">{{ item.position }}.</span>
                <RouterLink :to="{ name: 'song-detail', params: { songId: item.songId } }">{{ item.songTitle }}</RouterLink>
                <span class="music muted">{{ musicLabel(item) }}</span>
                <span v-if="!canWrite" class="duration">{{ formatDuration(item.durationMs) || '—' }}</span>
                <label v-else class="duration">
                  <span class="visually-hidden">{{ t('setlists.detail.durationOf', { songTitle: item.songTitle }) }}</span>
                  <input :value="formatDuration(item.manualDurationMs)" :placeholder="formatDuration(item.audioDurationMs) || 'm:ss'"
                         inputmode="numeric" :disabled="savingItems" :data-testid="`duration-${item.songId}`"
                         @change="onChangeDuration(item, $event)" />
                </label>

                <select v-if="canWrite" class="version" :value="item.versionId ?? ''" :disabled="savingItems"
                        :aria-label="t('setlists.detail.versionOf', { songTitle: item.songTitle })" @change="onChangeVersion(item, $event)">
                  <option value="">{{ t('setlists.detail.latestVersion') }}</option>
                  <option v-for="v in item.versions" :key="v.id" :value="v.id">v{{ v.number }} · {{ v.title }}</option>
                </select>
                <span v-else class="version muted">{{ playedLabel(item) }}</span>
                <input v-if="canWrite" class="notes" :value="item.notes ?? ''" :maxlength="SETLIST_NOTES_MAX"
                       :placeholder="t('setlists.detail.notesPlaceholder')" :disabled="savingItems" :aria-label="t('setlists.detail.notesFor', { songTitle: item.songTitle })"
                       @change="onChangeNotes(item, $event)" />
                <span v-else class="notes">{{ item.notes }}</span>
                <button v-if="canWrite" type="button" class="secondary" :disabled="savingItems"
                        :aria-label="t('setlists.detail.removeItem', { songTitle: item.songTitle })" @click="onRemoveItem(item)">{{ t('setlists.detail.remove') }}</button>
              </div>
            </template>
          </ReorderableList>
          <p v-if="savingItems" class="muted" role="status">{{ t('common.actions.saving') }}</p>

          <SongPicker v-if="canWrite" :in-setlist="songIds" :remaining="remaining" :disabled="savingItems" @add="onAdd" />
        </SectionCard>
      </div>

      <SetlistPrintSheet :setlist="setlist" />
    </template>
  </section>
</template>

<style scoped>
.actions { display: flex; gap: .75rem; align-items: center; flex-wrap: wrap; }
.total { margin-top: 0; }
.line {
  display: grid; grid-template-columns: 2rem minmax(0, 1fr) auto 5rem; gap: .4rem .5rem; align-items: center;
}
.line a { overflow-wrap: anywhere; font-weight: 600; }
.number { color: var(--muted); text-align: right; }
.music { font-size: .9rem; white-space: nowrap; }
.duration input { width: 100%; text-align: right; }
.duration { text-align: right; }
.version { grid-column: 2; min-width: 0; }
.notes { grid-column: 3 / 5; min-width: 0; }
.line button { grid-column: 2 / -1; justify-self: end; }
.between { margin: 0 0 .4rem 2.5rem; padding-left: .6rem; border-left: 3px solid var(--border); font-style: italic; }
.between p { margin: 0; white-space: pre-line; }
.between textarea { width: 100%; resize: vertical; font-style: italic; }
.add-comment {
  display: block; margin: 0 0 .25rem 2.5rem; background: none; border: none; padding: 0;
  color: var(--primary); font-size: .85rem; cursor: pointer;
}
.add-comment:disabled { color: var(--muted); cursor: default; }
@media (max-width: 640px) {
  .line { grid-template-columns: 2rem 1fr 5rem; }
  .music { grid-column: 2; }
  .version, .notes { grid-column: 2 / -1; }
}
</style>