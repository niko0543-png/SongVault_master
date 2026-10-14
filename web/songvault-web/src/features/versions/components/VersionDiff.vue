<script setup lang="ts">
import { computed, nextTick, ref, useTemplateRef, watch } from 'vue'
import type { SongVersion } from '../types'
import { diffFields, diffFiles } from '../diff'
import TextDiff from './TextDiff.vue'

/** Différences de A (avant) vers B (après) : champs, notes, paroles et fichiers (#9). */
const props = defineProps<{ a: SongVersion; b: SongVersion }>()

const fields = computed(() => diffFields(props.a, props.b))
const files = computed(() => diffFiles(props.a.files, props.b.files))
const filesChanged = computed(() => files.value.added.length + files.value.removed.length > 0)

// ================= Navigation clavier entre les changements =================
// Cibles : la ligne de chaque champ modifié, le début de chaque bloc modifié, la liste des fichiers
const root = useTemplateRef<HTMLElement>('root')
const targets = ref<HTMLElement[]>([])
const current = ref(-1)
const position = ref('')                  // annoncé par la zone aria-live

async function collect() {
  await nextTick()
  targets.value = [...(root.value?.querySelectorAll<HTMLElement>('[data-change]') ?? [])]
  current.value = -1
  position.value = ''
}
watch(() => [props.a.id, props.b.id], collect, { immediate: true })

function go(step: 1 | -1) {
  const count = targets.value.length
  if (count === 0) return
  current.value = current.value < 0 && step < 0 ? count - 1 : (current.value + step + count) % count
  const target = targets.value[current.value]!
  target.focus()
  target.scrollIntoView?.({ block: 'center' })
  position.value = `Changement ${current.value + 1} sur ${count}`
}
</script>

<template>
  <div ref="root" class="version-diff">
    <div class="diff-nav">
      <h2>Différences de v{{ a.number }} à v{{ b.number }}</h2>
      <template v-if="targets.length > 0">
        <button type="button" class="secondary" @click="go(-1)">Changement précédent</button>
        <button type="button" class="secondary" @click="go(1)">Changement suivant</button>
      </template>
      <p v-else class="muted">Aucune différence.</p>
      <p class="muted" role="status" aria-live="polite">{{ position }}</p>
    </div>

    <table class="fields">
      <caption class="visually-hidden">Champs de v{{ a.number }} et v{{ b.number }}</caption>
      <thead>
        <tr><th scope="col">Champ</th><th scope="col">v{{ a.number }}</th><th scope="col">v{{ b.number }}</th><th scope="col">État</th></tr>
      </thead>
      <tbody>
        <tr v-for="f in fields" :key="f.key" :class="f.changed ? 'changed' : 'same'"
            :data-change="f.changed ? '' : undefined" :tabindex="f.changed ? -1 : undefined">
          <th scope="row">{{ f.label }}</th>
          <td>{{ f.before }}</td>
          <td>{{ f.after }}</td>
          <td>{{ f.changed ? 'Modifié' : 'Identique' }}</td>
        </tr>
      </tbody>
    </table>

    <TextDiff title="Notes" :before="a.notes" :after="b.notes" />
    <TextDiff title="Paroles" :before="a.lyrics" :after="b.lyrics" />

    <section class="files" aria-labelledby="diff-files" :data-change="filesChanged ? '' : undefined"
             :tabindex="filesChanged ? -1 : undefined">
      <h3 id="diff-files">Fichiers <small v-if="!filesChanged" class="muted">· Identiques</small></h3>
      <ul>
        <li v-for="f in files.added" :key="f.id" class="file--added">
          <ins><span aria-hidden="true">+ </span><span class="visually-hidden">Ajouté : </span>{{ f.originalFileName }}</ins>
        </li>
        <li v-for="f in files.removed" :key="f.id" class="file--removed">
          <del><span aria-hidden="true">− </span><span class="visually-hidden">Retiré : </span>{{ f.originalFileName }}</del>
        </li>
        <li v-for="f in files.kept" :key="f.id" class="muted">{{ f.originalFileName }}</li>
      </ul>
      <p v-if="files.kept.length + files.added.length + files.removed.length === 0" class="muted">Aucun fichier.</p>
    </section>
  </div>
</template>

<style scoped>
.version-diff { display: grid; gap: 1.25rem; }
.diff-nav { display: flex; gap: .75rem; align-items: center; flex-wrap: wrap; }
.diff-nav h2 { margin: 0 auto 0 0; font-size: 1.15rem; }
.diff-nav p { margin: 0; }
.fields { width: 100%; border-collapse: collapse; }
.fields th, .fields td { text-align: left; padding: .4rem .5rem; border-bottom: 1px solid var(--border); }
.fields tr.same { color: var(--muted); }
.fields tr.changed { background: #fff8e1; font-weight: 600; }
.fields tr.changed td:last-child { color: #8a5a00; }
.files ul { margin: 0; padding-left: 1.25rem; }
.files ins, .files del { text-decoration: none; }
.file--added { color: #1e7d32; }
.file--removed { color: var(--danger); }
.file--removed del { text-decoration: line-through; }
h3 { margin: 0 0 .5rem; font-size: 1rem; }
</style>