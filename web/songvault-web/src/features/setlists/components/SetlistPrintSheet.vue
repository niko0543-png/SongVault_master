<script setup lang="ts">
import { computed } from 'vue'
import type { Setlist } from '../types'
import { formatDuration, formatSetlistDate, formatTotal } from '../format'

/**
 * Feuille de scène : invisible à l'écran, seule imprimée (main.css, #21). Gros caractères, une page :
 * une ligne par morceau (le texte trop long est coupé), et plus la setlist est longue, plus le texte
 * rétrécit, jusqu'à 50 morceaux sur une page A4 (vérifié en PDF avec Chromium).
 */
const props = defineProps<{ setlist: Setlist }>()

const size = computed(() => {
  const count = props.setlist.items.length
  if (count <= 12) return 'size-xl'
  if (count <= 20) return 'size-l'
  if (count <= 30) return 'size-m'
  if (count <= 40) return 'size-s'
  return 'size-xs'
})
</script>

<template>
  <section class="print-sheet" :class="size" data-testid="print-sheet">
    <header>
      <h1>{{ setlist.name }}</h1>
      <p>{{ formatSetlistDate(setlist.date) }}<template v-if="setlist.venue"> · {{ setlist.venue }}</template></p>
    </header>
    <ol>
      <li v-for="item in setlist.items" :key="item.songId">
        <span class="number">{{ item.position }}</span>
        <span class="title">{{ item.songTitle }}<small v-if="item.notes" class="notes"> — {{ item.notes }}</small></span>
        <span class="key">{{ item.key ?? '' }}</span>
        <span class="bpm">{{ item.bpm ? `${item.bpm} bpm` : '' }}</span>
        <span class="duration">{{ formatDuration(item.durationMs) }}</span>
      </li>
    </ol>
    <footer>
      Durée totale : {{ formatTotal(setlist.totalDurationMs) }}
      <template v-if="setlist.missingDurationCount > 0">
        (+ {{ setlist.missingDurationCount }} {{ setlist.missingDurationCount > 1 ? 'morceaux' : 'morceau' }} sans durée)
      </template>
    </footer>
  </section>
</template>

<style scoped>
.print-sheet { display: none; }

@media print {
  .print-sheet {
    display: block; color: #000; background: #fff; font-family: Arial, Helvetica, sans-serif; line-height: 1.15;
  }
  header { border-bottom: 2px solid #000; margin-bottom: .3em; }
  h1, .title { white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
  h1 { font-size: 1.5em; margin: 0; }
  header p { margin: .1em 0 .2em; font-size: .6em; }
  ol { list-style: none; margin: 0; padding: 0; }
  li {
    display: grid; grid-template-columns: 1.4em minmax(0, 1fr) 1.9em 3em 2.3em; gap: .35em; align-items: baseline;
    padding: .12em 0; border-bottom: 1px solid #999;
  }
  .number { font-weight: bold; text-align: right; }
  .title { font-weight: bold; }
  .notes { font-weight: normal; font-size: .6em; }
  .key, .bpm, .duration { font-size: .7em; text-align: right; }
  footer { margin-top: .5em; font-size: .7em; font-weight: bold; }

  .size-xl { font-size: 28pt; }
  .size-l { font-size: 22pt; }
  .size-m { font-size: 16pt; }
  .size-s { font-size: 12pt; }
  .size-xs { font-size: 10pt; }
}
</style>