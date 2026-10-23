import '@/assets/main.css'
import { createApp, defineAsyncComponent, h } from 'vue'
import { createPinia } from 'pinia'
import { useBandStore } from '@/features/bands/stores/bandStore'

/**
 * #30 : la comparaison chargée à la demande, entre demo.gp (A) et demo-b.gp (B).
 * #36 : comme sur la page de la tablature (« Afficher les modifs ») : liste à côté, partition en plein cadre.
 * Le test sert les fichiers à l'adresse de lecture habituelle, …/files/{id}/content, id = nom sans extension.
 */
const GuitarProCompare = defineAsyncComponent(() => import('@/features/guitarpro/components/GuitarProCompare.vue'))

const pinia = createPinia()
const app = createApp({
  render: () => h(GuitarProCompare, {
    songId: 'demo', a: { versionId: 'v1', fileId: 'demo' }, b: { versionId: 'v2', fileId: 'demo-b' }, label: 'riff.gp',
    style: 'height: 85vh',
  }),
})
app.use(pinia)
useBandStore(pinia).activeId = 'b1'
app.mount('#compare')
document.body.dataset.ready = 'true'
