<script setup lang="ts">
import { computed } from 'vue'
import { changeStarts, countChanges, diffLines } from '../diff'

/** Paroles ou notes, de A (before) vers B (after). */
const props = defineProps<{ title: string; before: string | null; after: string | null }>()

const lines = computed(() => diffLines(props.before, props.after))
const counts = computed(() => countChanges(lines.value))
const starts = computed(() => new Set(changeStarts(lines.value)))
const headingId = computed(() => `diff-${props.title.toLowerCase()}`)

const summary = computed(() => {
  const { added, removed } = counts.value
  if (lines.value.length === 0) return 'Vide des deux côtés'
  if (added === 0 && removed === 0) return 'Identiques'
  const plural = (n: number, word: string) => `${n} ligne${n > 1 ? 's' : ''} ${word}${n > 1 ? 's' : ''}`
  return [added && plural(added, 'ajoutée'), removed && plural(removed, 'supprimée')].filter(Boolean).join(', ')
})
</script>

<template>
  <section class="text-diff" :aria-labelledby="headingId">
    <h3 :id="headingId">{{ title }} <small class="muted">· {{ summary }}</small></h3>
    <ol v-if="counts.added + counts.removed > 0" class="diff" :aria-label="`Différences : ${title}`">
      <!-- data-change : début d'un bloc modifié, cible des boutons « changement précédent / suivant » -->
      <li v-for="(line, i) in lines" :key="i" :class="`line line--${line.kind}`"
          :data-change="starts.has(i) ? '' : undefined" :tabindex="starts.has(i) ? -1 : undefined">
        <ins v-if="line.kind === 'added'"><span class="sign" aria-hidden="true">+</span><span class="visually-hidden">Ajouté : </span>{{ line.text }}</ins>
        <del v-else-if="line.kind === 'removed'"><span class="sign" aria-hidden="true">−</span><span class="visually-hidden">Supprimé : </span>{{ line.text }}</del>
        <span v-else><span class="sign" aria-hidden="true"> </span>{{ line.text }}</span>
      </li>
    </ol>
    <p v-else-if="lines.length > 0" class="pre unchanged">{{ after }}</p>
  </section>
</template>

<style scoped>
h3 { margin: 0 0 .5rem; font-size: 1rem; }
.diff { list-style: none; margin: 0; padding: 0; font-family: ui-monospace, monospace; font-size: .9rem; }
.line { white-space: pre-wrap; overflow-wrap: anywhere; padding: 0 .5rem; min-height: 1.4em; }
.line ins, .line del { text-decoration: none; }
.line--added { background: #e6f4ea; border-left: 3px solid #1e7d32; }
.line--removed { background: #fdecea; border-left: 3px solid var(--danger); }
.line--removed del { text-decoration: line-through; text-decoration-thickness: 1px; }
.line--same { color: var(--muted); border-left: 3px solid transparent; }
.sign { display: inline-block; width: 1.25em; font-weight: 700; }
.unchanged { color: var(--muted); }
</style>