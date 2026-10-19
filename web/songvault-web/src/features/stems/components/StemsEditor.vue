<script setup lang="ts">
import { computed, ref } from 'vue'
import { STEM_NAME_MAX, STEMS_MAX, type Stem, type StemInput } from '../types'
import type { SongFile } from '@/features/files/types'
import ReorderableList from '@/shared/components/ReorderableList.vue'

/** Choix des pistes (#23) : quels fichiers audio de la version, sous quel nom, dans quel ordre. */
const props = defineProps<{ files: readonly SongFile[]; stems: readonly Stem[]; saving: boolean }>()
const emit = defineEmits<{ save: [stems: StemInput[]]; cancel: [] }>()

interface Row { fileId: string; name: string; fileName: string }

const rows = ref<Row[]>(props.stems.map((s) => ({ fileId: s.fileId, name: s.name, fileName: s.originalFileName })))
const audioFiles = computed(() => props.files.filter((f) => f.fileType === 'Audio'))
const remaining = computed(() => audioFiles.value.filter((f) => !rows.value.some((r) => r.fileId === f.id)))
const toAdd = ref('')

/** « voix-lead.wav » → « voix-lead » : un nom de départ que l'on corrige si besoin. */
function defaultName(fileName: string) {
  const dot = fileName.lastIndexOf('.')
  return (dot > 0 ? fileName.slice(0, dot) : fileName).slice(0, STEM_NAME_MAX)
}

function add() {
  const file = remaining.value.find((f) => f.id === toAdd.value) ?? remaining.value[0]
  if (!file || rows.value.length >= STEMS_MAX) return
  rows.value = [...rows.value, { fileId: file.id, name: defaultName(file.originalFileName), fileName: file.originalFileName }]
  toAdd.value = ''
}

function remove(row: Row) {
  rows.value = rows.value.filter((r) => r !== row)
}

const problem = computed(() =>
  rows.value.some((r) => r.name.trim() === '') ? 'Chaque piste a besoin d’un nom.' : null)

function onSubmit() {
  if (problem.value || props.saving) return
  emit('save', rows.value.map((r) => ({ fileId: r.fileId, name: r.name.trim() })))
}
</script>

<template>
  <form class="stems-editor" @submit.prevent="onSubmit">
    <p v-if="rows.length === 0" class="muted">Aucune piste choisie : le mélangeur sera retiré.</p>
    <ReorderableList v-else :items="rows" :item-key="(r) => r.fileId" :item-label="(r) => r.name || r.fileName"
                     label="Pistes du mélangeur" :disabled="saving" @move="(next) => (rows = next)">
      <template #default="{ item }">
        <div class="stem-row" data-testid="stem-row">
          <input v-model="item.name" :maxlength="STEM_NAME_MAX" :aria-label="`Nom de la piste ${item.fileName}`"
                 placeholder="Voix, Guitare, Batterie…" />
          <span class="muted file">{{ item.fileName }}</span>
          <button type="button" class="secondary" :aria-label="`Retirer ${item.name || item.fileName}`" @click="remove(item)">
            Retirer
          </button>
        </div>
      </template>
    </ReorderableList>

    <div v-if="remaining.length > 0 && rows.length < STEMS_MAX" class="add">
      <select v-model="toAdd" aria-label="Fichier audio à ajouter">
        <option value="">Choisir un fichier audio…</option>
        <option v-for="f in remaining" :key="f.id" :value="f.id">{{ f.originalFileName }}</option>
      </select>
      <button type="button" class="secondary" @click="add">Ajouter une piste</button>
    </div>
    <p v-else-if="rows.length >= STEMS_MAX" class="muted">{{ STEMS_MAX }} pistes au plus par version.</p>

    <p v-if="problem" class="field-error" role="alert">{{ problem }}</p>
    <div class="actions">
      <button type="button" class="secondary" :disabled="saving" @click="emit('cancel')">Annuler</button>
      <button type="submit" :disabled="!!problem || saving">{{ saving ? 'Enregistrement…' : 'Enregistrer les pistes' }}</button>
    </div>
  </form>
</template>

<style scoped>
.stem-row { display: grid; grid-template-columns: minmax(0, 14rem) minmax(0, 1fr) auto; gap: .5rem; align-items: center; }
.file { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.add { display: flex; gap: .5rem; margin: .75rem 0; }
.add select { flex: 1; min-width: 0; }
.actions { display: flex; gap: .75rem; justify-content: flex-end; }
@media (max-width: 600px) { .stem-row { grid-template-columns: 1fr auto; } .file { display: none; } }
</style>