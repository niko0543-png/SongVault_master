<script setup lang="ts">
import { computed } from 'vue'
import { parseSheet } from '../chordpro'

/**
 * #25 : paroles avec les accords au-dessus de leur syllabe (ADR 0032). Chaque mot est un bloc : sur un
 * petit écran, la ligne passe à la ligne entre deux mots, jamais entre un accord et sa syllabe.
 */
const props = defineProps<{ source: string }>()
const lines = computed(() => parseSheet(props.source))
</script>

<template>
  <div class="sheet">
    <template v-for="(line, i) in lines" :key="i">
      <p v-if="line.kind === 'lyrics'" class="line" :class="{ chorded: line.hasChords }">
        <span v-for="(word, j) in line.words" :key="j" class="word">
          <span v-for="(piece, k) in word" :key="k" class="piece">
            <span v-if="line.hasChords" class="chord">{{ piece.chord ?? '' }}</span>
            <span class="lyric">{{ piece.text }}</span>
          </span>
        </span>
      </p>
      <p v-else-if="line.kind === 'section'" class="section">{{ line.text }}</p>
      <p v-else-if="line.kind === 'comment'" class="comment">{{ line.text }}</p>
      <p v-else class="blank" aria-hidden="true" />
    </template>
  </div>
</template>

<style scoped>
.sheet { font-size: 1rem; line-height: 1.35; overflow-wrap: anywhere; }
.line { display: flex; flex-wrap: wrap; align-items: flex-start; margin: 0 0 .15rem; }
.line.chorded { margin-bottom: .35rem; }
.word { display: inline-flex; align-items: flex-start; max-width: 100%; min-width: 0; }
.piece { display: inline-flex; flex-direction: column; min-width: 0; }
.chord {
  height: 1.25em; line-height: 1.25; padding-right: .35em; white-space: pre;   /* même hauteur avec ou sans accord */
  font-weight: 700; font-size: .85em; color: var(--primary);
}
/* break-spaces : les espaces gardent leur largeur, et un mot plus large que l'écran se coupe quand même */
.lyric { white-space: break-spaces; }
.section { margin: .75rem 0 .25rem; font-weight: 700; }
.comment { margin: .5rem 0 .25rem; font-style: italic; color: var(--muted); }
.blank { margin: 0; height: .9em; }
</style>