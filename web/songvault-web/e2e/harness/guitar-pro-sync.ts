import '@/assets/main.css'
import { createApp, defineAsyncComponent, h } from 'vue'
import { createPinia } from 'pinia'
import { useBandStore } from '@/features/bands/stores/bandStore'

/**
 * #29 : le lecteur chargé comme dans FileList, avec une maquette (fichier audio « demo ») dans la version.
 * ?file=saved : la tablature est déjà calée ; ?file=fresh : elle ne l'est pas. Le test répond aux appels de l'API.
 */
const fileId = new URLSearchParams(location.search).get('file') ?? 'fresh'
const GuitarProViewer = defineAsyncComponent(() => import('@/features/guitarpro/components/GuitarProViewer.vue'))

const pinia = createPinia()
const app = createApp({
  render: () => h(GuitarProViewer, {
    src: `/api/bands/b1/songs/s1/versions/v1/files/${fileId}/content`,
    label: 'riff.gp5',
    sync: { songId: 's1', versionId: 'v1', fileId, audios: [{ id: 'demo', name: 'maquette.wav' }], canEdit: true },
  }),
})
app.use(pinia)
useBandStore(pinia).activeId = 'b1'
app.mount('#viewer')
document.body.dataset.ready = 'true'