import '@/assets/main.css'
import { createApp, defineAsyncComponent, h } from 'vue'
import { createPinia } from 'pinia'

/**
 * #28 : le lecteur chargé comme dans FileList (à la demande), sur le fichier donné par ?file=demo.gp5 ou demo.gp.
 * window.alphaTabLoaded dit au test si alphaTab a déjà été téléchargé.
 */
const params = new URLSearchParams(location.search)
const file = params.get('file') ?? 'demo.gp5'
// #32 : ?fill=1 = comme sur la page du lecteur (TablaturePage) : le lecteur remplit un cadre fixe de la taille de l'écran
const fill = params.get('fill') === '1'
const GuitarProViewer = defineAsyncComponent(() => import('@/features/guitarpro/components/GuitarProViewer.vue'))

const viewer = () => h(GuitarProViewer, { src: `/e2e/fixtures/${file}`, label: file, fill })
createApp({ render: () => (fill ? h('div', { style: 'position: fixed; inset: 0; display: flex; flex-direction: column' }, [viewer()]) : viewer()) })
  .use(createPinia())
  .mount('#viewer')
document.body.dataset.ready = 'true'