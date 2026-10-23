import '@/assets/main.css'
import { createApp, h } from 'vue'
import ChordSheet from '@/features/chords/components/ChordSheet.vue'

/** #25 : des paroles qui passent à la ligne à 360 px, avec un mot plus large que l'écran. */
export const SOURCE = [
  '{c: Couplet 1}',
  '[G]Je marche [D/F#]seul sous la [Em7]pluie qui ne s’ar[Cadd9]rête ja[D]mais',
  'Et la [Cadd9]ville se [D]tait, im[G]mobile, en[Em]dormie sous les ré[A7sus4]verbères',
  '',
  '[Refrain]',
  '[G]Anticonstitutionnellementetdéfinitivementinsupportable [D]fin',
].join('\n')

createApp({ render: () => h(ChordSheet, { source: SOURCE }) }).mount('#sheet')
document.body.dataset.ready = 'true'