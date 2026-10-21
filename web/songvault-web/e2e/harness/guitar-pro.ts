import '@/assets/main.css'
import { createApp, defineAsyncComponent, h } from 'vue'
import { createPinia } from 'pinia'

/**
 * #28 : le lecteur chargé comme dans FileList (à la demande), sur le fichier donné par ?file=demo.gp5 ou demo.gp.
 * window.alphaTabLoaded dit au test si alphaTab a déjà été téléchargé.
 */
const file = new URLSearchParams(location.search).get('file') ?? 'demo.gp5'
const GuitarProViewer = defineAsyncComponent(() => import('@/features/guitarpro/components/GuitarProViewer.vue'))

createApp({ render: () => h(GuitarProViewer, { src: `/e2e/fixtures/${file}`, label: file }) })
  .use(createPinia())
  .mount('#viewer')
document.body.dataset.ready = 'true'