import { markRaw, ref, shallowRef, toValue, watch, type MaybeRefOrGetter } from 'vue'
import { importer, Settings, type model } from '@coderline/alphatab'
import { filesApi } from '@/features/files/api/filesApi'
import { toPlainScore } from './plain'
import { runDiff } from './runDiff'
import { highlight, NEW_COLORS, OLD_COLORS } from './highlight'
import type { ScoreDiff } from './scoreDiff'

/** #36 : un fichier Guitar Pro d'une version, l'un des deux côtés d'une comparaison. */
export interface ComparedFile {
  versionId: string
  fileId: string
}

/**
 * #30 : lit les deux fichiers, calcule les différences de A vers B (dans un Web Worker) et colore la partition de B.
 * #36 : partagé par le résumé de l'écran A/B et par la page de la tablature, qui montre les différences.
 * #37 : avec `withOld`, colore aussi la partition de A : en rouge ce qui a été retiré, en orange ce qui a été
 * remplacé (différences de B vers A).
 */
export function useScoreCompare(songId: MaybeRefOrGetter<string>, a: MaybeRefOrGetter<ComparedFile | null>, b: MaybeRefOrGetter<ComparedFile | null>,
  withOld = false) {
  const status = ref<'loading' | 'ready' | 'error'>('loading')
  const diff = shallowRef<ScoreDiff | null>(null)
  const scoreA = shallowRef<model.Score | null>(null)
  const scoreB = shallowRef<model.Score | null>(null)

  async function read(file: ComparedFile): Promise<model.Score> {
    const response = await fetch(filesApi.contentUrl(toValue(songId), file.versionId, file.fileId))   // suit la redirection vers le stockage
    if (!response.ok) throw new Error(String(response.status))
    return importer.ScoreLoader.loadScoreFromBytes(new Uint8Array(await response.arrayBuffer()), new Settings())
  }

  let run = 0
  async function compare() {
    const id = ++run
    status.value = 'loading'
    const fileA = toValue(a)
    const fileB = toValue(b)
    if (!fileA || !fileB) return
    try {
      const [before, after] = await Promise.all([read(fileA), read(fileB)])
      const plainA = toPlainScore(before)
      const plainB = toPlainScore(after)
      const [result, reverse] = await Promise.all([runDiff(plainA, plainB), withOld ? runDiff(plainB, plainA) : null])
      if (id !== run) return                                              // un autre fichier a été choisi entre-temps
      highlight(after, result, NEW_COLORS)
      if (reverse) highlight(before, reverse, OLD_COLORS)
      diff.value = result
      scoreA.value = markRaw(before)
      scoreB.value = markRaw(after)
      status.value = 'ready'
    } catch {
      if (id === run) status.value = 'error'
    }
  }

  watch(() => [toValue(songId), toValue(a)?.versionId, toValue(a)?.fileId, toValue(b)?.versionId, toValue(b)?.fileId], compare, { immediate: true })

  return { status, diff, scoreA, scoreB }
}
