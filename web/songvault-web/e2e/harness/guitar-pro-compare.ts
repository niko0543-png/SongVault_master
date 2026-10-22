import '@/assets/main.css'
import { createApp, defineAsyncComponent, h } from 'vue'
import { createPinia } from 'pinia'
import { useBandStore } from '@/features/bands/stores/bandStore'
import type { SongFile } from '@/features/files/types'
import type { SongVersion } from '@/features/versions/types'

/**
 * #30 : la comparaison chargée comme dans l'écran A/B (à la demande), entre demo.gp (A) et demo-b.gp (B).
 * Le test sert les fichiers à l'adresse de lecture habituelle, …/files/{id}/content, id = nom sans extension.
 */
const file = (id: string): SongFile => ({
  id, originalFileName: 'riff.gp', contentType: 'application/octet-stream', sizeBytes: 3300,
  fileType: 'Tablature', uploadedAt: '2026-10-21T10:00:00Z', preview: null, scan: 'Clean',
})
const version = (id: string, number: number, fileId: string): SongVersion => ({
  id, songId: 'demo', number, title: `v${number}`, status: 'Demo', notes: null, lyrics: null, bpm: null, key: null,
  createdAt: '2026-10-21T10:00:00Z', updatedAt: '2026-10-21T10:00:00Z', files: [file(fileId)],
})
const GuitarProCompare = defineAsyncComponent(() => import('@/features/guitarpro/components/GuitarProCompare.vue'))

const pinia = createPinia()
const app = createApp({ render: () => h(GuitarProCompare, { songId: 'demo', a: version('v1', 1, 'demo'), b: version('v2', 2, 'demo-b') }) })
app.use(pinia)
useBandStore(pinia).activeId = 'b1'
app.mount('#compare')
document.body.dataset.ready = 'true'