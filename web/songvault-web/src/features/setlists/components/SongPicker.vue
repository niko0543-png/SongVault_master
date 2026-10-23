<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { songsApi } from '@/features/songs/api/songsApi'
import type { Song } from '@/features/songs/types'
import { SETLIST_MAX_ITEMS } from '../types'
import { getErrorMessage } from '@/shared/api/ApiError'
import { t } from '@/shared/i18n'

/**
 * Choix de morceaux existants pour une setlist (ADR 0046) : tous les morceaux du groupe, sortis compris
 * (on joue aussi ses morceaux sortis en concert), filtrés par titre ou artiste. On coche, puis on ajoute
 * la sélection d'un coup, en fin de setlist et dans l'ordre des cases cochées.
 */
const props = defineProps<{
  /** Morceaux déjà dans la setlist : cochés et grisés */
  inSetlist: readonly string[]
  /** Places libres dans la setlist : on ne coche pas au-delà */
  remaining: number
  disabled?: boolean
}>()
const emit = defineEmits<{ add: [songs: Song[]] }>()

const PAGE_SIZE = 50                 // le maximum de l'API (ListSongsHandler.MaxPageSize)

const search = ref('')
const songs = ref<Song[]>([])
const page = ref(1)
const totalPages = ref(0)
const isLoading = ref(false)
const error = ref<string | null>(null)
/** Cases cochées, dans l'ordre où on les a cochées */
const selected = ref<Song[]>([])

const isFull = computed(() => props.remaining <= 0)
/** La sélection remplit déjà les places libres */
const noRoom = computed(() => selected.value.length >= props.remaining)
const hasMore = computed(() => page.value < totalPages.value)
const isSelected = (song: Song) => selected.value.some((s) => s.id === song.id)
const isInSetlist = (song: Song) => props.inSetlist.includes(song.id)

let requestId = 0
async function load(nextPage: number) {
  const id = ++requestId
  isLoading.value = true
  error.value = null
  try {
    const result = await songsApi.list({ search: search.value.trim() || undefined, page: nextPage, pageSize: PAGE_SIZE })
    if (id !== requestId) return                      // une frappe plus récente a relancé la recherche
    songs.value = nextPage === 1 ? result.items : [...songs.value, ...result.items]
    page.value = result.page
    totalPages.value = result.totalPages
  } catch (e) {
    if (id === requestId) error.value = getErrorMessage(e)
  } finally {
    if (id === requestId) isLoading.value = false
  }
}

// La liste se filtre pendant la frappe, sans attendre un clic sur « Chercher »
let timer: ReturnType<typeof setTimeout> | undefined
watch(search, () => {
  clearTimeout(timer)
  timer = setTimeout(() => void load(1), 250)
})
onBeforeUnmount(() => clearTimeout(timer))
void load(1)

function toggle(song: Song, event: Event) {
  const checked = (event.target as HTMLInputElement).checked
  if (!checked) selected.value = selected.value.filter((s) => s.id !== song.id)
  else if (!noRoom.value) selected.value = [...selected.value, song]
  else (event.target as HTMLInputElement).checked = false
}

function onAdd() {
  if (selected.value.length === 0) return
  emit('add', selected.value)
  selected.value = []
}

// Un morceau ajouté entre-temps (par exemple depuis un autre onglet) ne reste pas coché
watch(() => props.inSetlist, (ids) => { selected.value = selected.value.filter((s) => !ids.includes(s.id)) })
</script>

<template>
  <div class="picker" data-testid="song-picker">
    <label for="setlist-search">{{ t('setlists.picker.title') }}</label>
    <input id="setlist-search" v-model="search" type="search" :placeholder="t('setlists.picker.searchPlaceholder')"
           :disabled="isFull" />
    <p v-if="isFull" class="muted">{{ t('setlists.detail.maxItems', { setlistMaxItems: SETLIST_MAX_ITEMS }) }}</p>
    <p v-if="error" class="field-error" role="alert">{{ error }}</p>

    <ul class="songs" :aria-label="t('setlists.picker.listLabel')" :aria-busy="isLoading">
      <li v-for="song in songs" :key="song.id">
        <label :class="{ muted: isInSetlist(song) }">
          <input type="checkbox" :checked="isInSetlist(song) || isSelected(song)"
                 :disabled="disabled || isInSetlist(song) || (noRoom && !isSelected(song))"
                 :data-testid="`pick-${song.id}`" @change="toggle(song, $event)" />
          <span class="title">{{ song.title }}<small v-if="song.artist" class="muted"> · {{ song.artist }}</small></span>
          <small v-if="isInSetlist(song)" class="tag">{{ t('setlists.picker.alreadyIn') }}</small>
          <small v-else-if="song.releaseId" class="tag">{{ t('setlists.picker.released') }}</small>
        </label>
      </li>
      <li v-if="!isLoading && songs.length === 0" class="muted">{{ t('setlists.picker.none') }}</li>
    </ul>
    <p v-if="isLoading" class="muted" role="status">{{ t('setlists.picker.loading') }}</p>
    <button v-else-if="hasMore" type="button" class="secondary more" @click="load(page + 1)">{{ t('setlists.picker.more') }}</button>

    <div class="add-row">
      <span class="muted" role="status">{{ t('setlists.picker.selected', { count: selected.length }) }}</span>
      <button type="button" :disabled="disabled || selected.length === 0" data-testid="pick-add" @click="onAdd">
        {{ t('setlists.picker.add', { count: selected.length }) }}
      </button>
    </div>
  </div>
</template>

<style scoped>
.picker { margin-top: 1rem; display: grid; gap: .4rem; }
.songs {
  list-style: none; padding: .25rem .5rem; margin: 0; display: grid; gap: .15rem;
  max-height: 18rem; overflow-y: auto; border: 1px solid var(--border); border-radius: var(--radius);
}
.songs label { display: flex; align-items: center; gap: .5rem; cursor: pointer; padding: .2rem 0; }
.songs input { flex: none; }
.title { flex: 1; min-width: 0; overflow-wrap: anywhere; }
.tag { flex: none; color: var(--muted); border: 1px solid var(--border); border-radius: 999px; padding: 0 .45rem; }
.more { justify-self: start; }
.add-row { display: flex; justify-content: space-between; align-items: center; gap: .5rem; flex-wrap: wrap; }
</style>
