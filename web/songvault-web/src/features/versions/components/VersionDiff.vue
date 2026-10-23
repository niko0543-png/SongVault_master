<script setup lang="ts">
import { computed, nextTick, ref, useTemplateRef, watch } from 'vue'
import type { SongVersion } from '../types'
import type { SongFile } from '@/features/files/types'
import { diffFields, diffFiles } from '../diff'
import TextDiff from './TextDiff.vue'
import { t } from '@/shared/i18n'

/**
 * Différences de A (avant) vers B (après) : champs, notes, paroles et fichiers (#9).
 * #36 : l'emplacement `file` reçoit chaque fichier de B (ajouté ou gardé), pour y montrer ce qui a changé dedans.
 */
const props = defineProps<{ a: SongVersion; b: SongVersion }>()
defineSlots<{ file?: (props: { file: SongFile }) => unknown }>()

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
  position.value = t('versions.diff.change', { current: current.value + 1, count })
}
</script>

<template>
  <div ref="root" class="version-diff">
    <div class="diff-nav">
      <h2>{{ t('versions.diff.title', { number: a.number, number2: b.number }) }}</h2>
      <template v-if="targets.length > 0">
        <button type="button" class="secondary" @click="go(-1)">{{ t('versions.diff.previous') }}</button>
        <button type="button" class="secondary" @click="go(1)">{{ t('versions.diff.next') }}</button>
      </template>
      <p v-else class="muted">{{ t('versions.diff.none') }}</p>
      <p class="muted" role="status" aria-live="polite">{{ position }}</p>
    </div>

    <table class="fields">
      <caption class="visually-hidden">{{ t('versions.diff.fields', { number: a.number, number2: b.number }) }}</caption>
      <thead>
        <tr><th scope="col">{{ t('versions.diff.field') }}</th><th scope="col">v{{ a.number }}</th><th scope="col">v{{ b.number }}</th><th scope="col">{{ t('versions.diff.state') }}</th></tr>
      </thead>
      <tbody>
        <tr v-for="f in fields" :key="f.key" :class="f.changed ? 'changed' : 'same'"
            :data-change="f.changed ? '' : undefined" :tabindex="f.changed ? -1 : undefined">
          <th scope="row">{{ f.label }}</th>
          <td>{{ f.before }}</td>
          <td>{{ f.after }}</td>
          <td>{{ f.changed ? t('versions.diff.changed') : t('versions.diff.same') }}</td>
        </tr>
      </tbody>
    </table>

    <TextDiff :title="t('versions.diff.notes')" :before="a.notes" :after="b.notes" />
    <TextDiff :title="t('versions.diff.lyrics')" :before="a.lyrics" :after="b.lyrics" />

    <section class="files" aria-labelledby="diff-files" :data-change="filesChanged ? '' : undefined"
             :tabindex="filesChanged ? -1 : undefined">
      <h3 id="diff-files">{{ t('versions.diff.files') }} <small v-if="!filesChanged" class="muted">{{ t('versions.diff.filesSame') }}</small></h3>
      <ul>
        <li v-for="f in files.added" :key="f.id" class="file--added">
          <ins><span aria-hidden="true">+ </span><span class="visually-hidden">{{ t('versions.diff.added') }} </span>{{ f.originalFileName }}</ins>
          <slot name="file" :file="f" />
        </li>
        <li v-for="f in files.removed" :key="f.id" class="file--removed">
          <del><span aria-hidden="true">− </span><span class="visually-hidden">{{ t('versions.diff.removed') }} </span>{{ f.originalFileName }}</del>
        </li>
        <li v-for="f in files.kept" :key="f.id"><span class="muted">{{ f.originalFileName }}</span><slot name="file" :file="f" /></li>
      </ul>
      <p v-if="files.kept.length + files.added.length + files.removed.length === 0" class="muted">{{ t('versions.diff.noFiles') }}</p>
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