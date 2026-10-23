import { markRaw, ref, shallowRef, toValue, watch, type MaybeRefOrGetter } from 'vue'
import { importer, Settings, type model } from '@coderline/alphatab'
import { filesApi } from '@/features/files/api/filesApi'
import { toPlainScore } from './plain'
import { runDiff } from './runDiff'
import { highlight } from './highlight'
import type { ScoreDiff } from './scoreDiff'

/** #36 : un fichier Guitar Pro d'une version, l'un des deux côtés d'une comparaison. */
export interface ComparedFile {
  versionId: string
  fileId: string
}

/**
 * #30 : lit les deux fichiers, calcule les différences de A vers B (dans un Web Worker) et colore la partition de B.
 * #36 : partagé par le résumé de l'écran A/B et par la page de la tablature, qui montre les différences.
 */
export function useScoreCompare(songId: MaybeRefOrGetter<string>, a: MaybeRefOrGetter<ComparedFile | null>, b: MaybeRefOrGetter<ComparedFile | null>) {
  const status = ref<'loading' | 'ready' | 'error'>('loading')
  const diff = shallowRef<ScoreDiff | null>(null)
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
      const [scoreA, score] = await Promise.all([read(fileA), read(fileB)])
      const result = await runDiff(toPlainScore(scoreA), toPlainScore(score))
      if (id !== run) return                                              // un autre fichier a été choisi entre-temps
      highlight(score, result)
      diff.value = result
      scoreB.value = markRaw(score)
      status.value = 'ready'
    } catch {
      if (id === run) status.value = 'error'
    }
  }

  watch(() => [toValue(songId), toValue(a)?.versionId, toValue(a)?.fileId, toValue(b)?.versionId, toValue(b)?.fileId], compare, { immediate: true })

  return { status, diff, scoreB }
}
