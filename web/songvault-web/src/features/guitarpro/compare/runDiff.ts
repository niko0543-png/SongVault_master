import type { PlainScore } from './plain'
import { diffScores, type ScoreDiff } from './scoreDiff'

/**
 * #30 : lance le calcul dans un Web Worker ; sans Worker (tests, vieux navigateur), le fait sur place.
 * Le worker est un module ES, comme ceux d'alphaTab (vite.config.ts, worker.format).
 */
export function runDiff(a: PlainScore, b: PlainScore): Promise<ScoreDiff> {
  if (typeof Worker === 'undefined') return Promise.resolve(diffScores(a, b))
  return new Promise((resolve, reject) => {
    const worker = new Worker(new URL('./compare.worker.ts', import.meta.url), { type: 'module' })
    worker.onmessage = (event: MessageEvent<ScoreDiff>) => {
      resolve(event.data)
      worker.terminate()
    }
    worker.onerror = (event) => {
      reject(new Error(event.message))
      worker.terminate()
    }
    worker.postMessage({ a, b })
  })
}